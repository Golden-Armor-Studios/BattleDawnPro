"use strict";


const functions = require('firebase-functions');
const admin = require('firebase-admin');
const { initializeApp, applicationDefault, getApp, getApps } = require('firebase-admin/app');
const { getFirestore, FieldValue } = require('firebase-admin/firestore');
const { CloudTasksClient } = require('@google-cloud/tasks');

const DEFAULT_PROJECT_ID = "goldenarmorstudios";
const FIRESTORE_DATABASE_ID = "battledawnpro";

function resolveProjectId() {
  return (
    process.env.GCLOUD_PROJECT ||
    process.env.GOOGLE_CLOUD_PROJECT ||
    DEFAULT_PROJECT_ID
  );
}

function ensureAdmin() {
  if (!getApps().length) {
    const projectId = resolveProjectId();
    initializeApp({
      credential: applicationDefault(),
      projectId
    });
  }
  return getApp();
}

function getDb() {
  const app = ensureAdmin();
  return getFirestore(app, FIRESTORE_DATABASE_ID);
}

let firestore = null;

function ensureDb() {
  if (!firestore) {
    firestore = getDb();
  }
  return firestore;
}

const RUNNING_IN_GCF = Boolean(process.env.K_SERVICE || process.env.FUNCTION_TARGET);
if (RUNNING_IN_GCF) {
  ensureAdmin();
}

const tasksClient = new CloudTasksClient();

const MAX_BATCH_WRITES = 450;
const BATCH_DELAY_MS = 25;
const MAX_CHUNK_DOCS = 200000;
const MAX_TILES_PER_CHUNK = 16384;
const TASK_LOCATION = "us-west2";
const TASK_QUEUE_NAME = "BattleDawnPro-SaveMap";

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function getTasksServiceAccountEmail(projectId) {
  const fromEnv = (process.env.TASKS_SERVICE_ACCOUNT_EMAIL || "").trim();
  if (fromEnv) return fromEnv;
  try {
    const cfg = functions.config();
    if (cfg && cfg.tasks && cfg.tasks.service_account_email) {
      return cfg.tasks.service_account_email;
    }
  } catch (_) {
    // functions config not available locally
  }
  return `${projectId}@appspot.gserviceaccount.com`;
}

async function ensureQueue(projectId) {
  const queuePath = tasksClient.queuePath(projectId, TASK_LOCATION, TASK_QUEUE_NAME);
  const locationPath = tasksClient.locationPath(projectId, TASK_LOCATION);
  const desiredRateLimits = {
    maxDispatchesPerSecond: 200,
    maxConcurrentDispatches: 50
  };

  try {
    const [existingQueue] = await tasksClient.getQueue({name: queuePath});
    const needsUpdate =
      !existingQueue.rateLimits ||
      existingQueue.rateLimits.maxDispatchesPerSecond !== desiredRateLimits.maxDispatchesPerSecond ||
      existingQueue.rateLimits.maxConcurrentDispatches !== desiredRateLimits.maxConcurrentDispatches;

    if (needsUpdate) {
      await tasksClient.updateQueue({
        queue: {
          name: queuePath,
          rateLimits: desiredRateLimits
        },
        updateMask: {
          paths: ["rate_limits.max_dispatches_per_second", "rate_limits.max_concurrent_dispatches"]
        }
      });
      functions.logger.info("Updated Cloud Tasks queue rate limits", {queuePath, desiredRateLimits});
    }
  } catch (error) {
    if (error.code === 5) {
      await tasksClient.createQueue({
        parent: locationPath,
        queue: {
          name: queuePath,
          rateLimits: desiredRateLimits
        }
      });
      functions.logger.info("Created Cloud Tasks queue with custom rate limits", {queuePath, desiredRateLimits});
    } else {
      throw error;
    }
  }

  return queuePath;
}

function ensureAuthenticated(context) {
  if (!context.auth) {
    throw new functions.https.HttpsError("unauthenticated", "Authentication is required.");
  }
}

function validateChunk(chunk, chunkSize) {
  if (!chunk || typeof chunk !== "object") {
    throw new functions.https.HttpsError("invalid-argument", "Each chunk must be an object.");
  }

  const { id } = chunk;
  const tiles = chunk.tiles || chunk.Tiles;
  if (typeof id !== "string" || id.trim().length === 0) {
    throw new functions.https.HttpsError("invalid-argument", "Each chunk requires a non-empty string id.");
  }

  if (!Array.isArray(tiles)) {
    throw new functions.https.HttpsError("invalid-argument", `Chunk '${id}' is missing its tiles array.`);
  }

  if (tiles.length > MAX_TILES_PER_CHUNK) {
    throw new functions.https.HttpsError(
      "invalid-argument",
      `Chunk '${id}' exceeds the maximum supported tile count of ${MAX_TILES_PER_CHUNK}.`
    );
  }

  for (const tile of tiles) {
    if (!tile || typeof tile !== "object") {
      throw new functions.https.HttpsError("invalid-argument", `Chunk '${id}' contains an invalid tile payload.`);
    }

    const x = tile.x ?? tile.X;
    const y = tile.y ?? tile.Y;
    const tileName = tile.tileName ?? tile.TileName;
    const tileObjectPath = tile.tileObjectPath ?? tile.TileObjectPath;
    const rawLayer = tile.tileLayer ?? tile.TileLayer;
    const tileLayer = typeof rawLayer === "string" && rawLayer.trim().length > 0 ? rawLayer.trim() : "Overlay";
    const transform = tile.transform ?? tile.Transform ?? null;

    if (typeof x !== "number" || typeof y !== "number") {
      throw new functions.https.HttpsError("invalid-argument", `Chunk '${id}' has tiles without numeric coordinates.`);
    }

    if (typeof tileLayer !== "string" || tileLayer.trim().length === 0) {
      throw new functions.https.HttpsError("invalid-argument", `Chunk '${id}' contains tiles without a tileLayer value.`);
    }

    if (
      (tileName !== null && typeof tileName !== "string") ||
      (tileObjectPath !== null && typeof tileObjectPath !== "string")
    ) {
      throw new functions.https.HttpsError(
        "invalid-argument",
        `Chunk '${id}' contains tiles with invalid tileName or tileObjectPath values.`
      );
    }

    if (transform !== null) {
      if (!Array.isArray(transform) || transform.length !== 16 || transform.some((value) => typeof value !== "number" || !Number.isFinite(value))) {
        throw new functions.https.HttpsError(
          "invalid-argument",
          `Chunk '${id}' contains tiles with an invalid transform array.`
        );
      }
    }
  }

  if (chunkSize && (chunkSize <= 0 || chunkSize > 1024)) {
    throw new functions.https.HttpsError("invalid-argument", "chunkSize must be between 1 and 1024.");
  }
}

async function commitOperationsInBatches(operations, label) {
  if (!operations.length) {
    return;
  }

  let batch = firestore.batch();
  let writesInBatch = 0;
  let processed = 0;

  for (const op of operations) {
    op(batch);
    writesInBatch += 1;
    processed += 1;

    if (writesInBatch >= MAX_BATCH_WRITES) {
      await batch.commit();
      batch = firestore.batch();
      writesInBatch = 0;
      await delay(BATCH_DELAY_MS);
    }

    if (processed % 250 === 0) {
      functions.logger.debug(`${label}: committed ${processed}/${operations.length}`);
    }
  }

  if (writesInBatch > 0) {
    await batch.commit();
  }
}

const saveMap = functions
  .region("us-central1")
  .runWith({ timeoutSeconds: 540, memory: "1GB" })
  .https.onCall(async (data, context) => {
    try {
      // Ensure Firestore is initialized
      ensureDb();
      ensureAuthenticated(context);

      if (!data || typeof data !== "object") {
        throw new functions.https.HttpsError("invalid-argument", "Request payload must be an object.");
      }

      const {
        mapId,
        planetName,
        planetSurface,
        planetSize,
        chunkSize = 32,
        tileCount = 0,
        chunks = [],
        deleteMissingChunks = true
      } = data;
      functions.logger.info("saveMap received request", {
        mapId,
        planetSize,
        chunkSize,
        tileCount,
        chunkCount: Array.isArray(chunks) ? chunks.length : undefined,
        deleteMissingChunks
      });
    const chunkIdsOverride = data.chunkIds;

    if (typeof mapId !== "string" || mapId.trim().length === 0) {
      throw new functions.https.HttpsError("invalid-argument", "mapId must be a non-empty string.");
    }

    if (typeof planetSize !== "number" || planetSize <= 0 || planetSize > 10000) {
      throw new functions.https.HttpsError("invalid-argument", "planetSize must be a positive number (<= 10000).");
    }

    if (!Array.isArray(chunks)) {
      throw new functions.https.HttpsError("invalid-argument", "chunks must be an array.");
    }

    if (chunks.length > MAX_CHUNK_DOCS) {
      throw new functions.https.HttpsError(
        "invalid-argument",
        `Too many chunk documents (${chunks.length}). Maximum supported is ${MAX_CHUNK_DOCS}.`
      );
    }

      let chunkIdsOverrideSet = null;
      if (chunkIdsOverride !== undefined) {
        if (!Array.isArray(chunkIdsOverride)) {
          throw new functions.https.HttpsError("invalid-argument", "chunkIds must be an array when provided.");
        }
        chunkIdsOverrideSet = new Set();
        for (const id of chunkIdsOverride) {
          if (typeof id !== "string" || id.trim().length === 0) {
            throw new functions.https.HttpsError("invalid-argument", "chunkIds must contain non-empty strings.");
          }
          chunkIdsOverrideSet.add(id);
          if (chunkIdsOverrideSet.size > MAX_CHUNK_DOCS) {
            throw new functions.https.HttpsError(
              "invalid-argument",
              `Too many chunk ids (${chunkIdsOverrideSet.size}). Maximum supported is ${MAX_CHUNK_DOCS}.`
            );
          }
        }
      }

    const sanitizedChunks = [];
    const incomingChunkIds = new Set();

    for (const chunk of chunks) {
      validateChunk(chunk, chunkSize);
      if (incomingChunkIds.has(chunk.id)) {
        throw new functions.https.HttpsError("invalid-argument", `Duplicate chunk id '${chunk.id}'.`);
      }
      incomingChunkIds.add(chunk.id);
      const tilesArray = (chunk.tiles || chunk.Tiles || []).map((tile) => {
        const x = tile.x ?? tile.X;
        const y = tile.y ?? tile.Y;
        const tileName = tile.tileName ?? tile.TileName ?? null;
        const tileObjectPath = tile.tileObjectPath ?? tile.TileObjectPath ?? null;
        const rawLayer = tile.tileLayer ?? tile.TileLayer;
        const layer = typeof rawLayer === "string" && rawLayer.trim().length > 0 ? rawLayer.trim() : "Overlay";
        const transform = tile.transform ?? tile.Transform ?? null;
        const normalizedTransform = Array.isArray(transform) && transform.length === 16 ? transform.map((value) => Number(value)) : null;
        return {
          x,
          y,
          TileName: tileName,
          TileObjectPath: tileObjectPath,
          TileLayer: layer,
          Transform: normalizedTransform
        };
      });
      sanitizedChunks.push({id: chunk.id, tiles: tilesArray});
    }

    let targetChunkIdSet;
    if (chunkIdsOverrideSet) {
      for (const chunkId of incomingChunkIds) {
        chunkIdsOverrideSet.add(chunkId);
      }
      targetChunkIdSet = chunkIdsOverrideSet;
    } else {
      targetChunkIdSet = incomingChunkIds;
    }

    if (targetChunkIdSet.size > MAX_CHUNK_DOCS) {
      throw new functions.https.HttpsError(
        "invalid-argument",
        `Too many chunk ids (${targetChunkIdSet.size}). Maximum supported is ${MAX_CHUNK_DOCS}.`
      );
    }

  const mapRef = firestore.collection("maps").doc(mapId);
  const mapDataRef = firestore.collection("MapData").doc(mapId);
    const chunksCollection = mapDataRef.collection("Chunks");

    const now = FieldValue.serverTimestamp();

    const projectOptions = ensureAdmin().options || {};
    const projectId = projectOptions.projectId || resolveProjectId();
    if (!projectId) {
      throw new functions.https.HttpsError("internal", "Unable to determine project id for Cloud Tasks.");
    }

    const queuePath = await ensureQueue(projectId);
    const taskHandlerUrl = `https://${TASK_LOCATION}-${projectId}.cloudfunctions.net/processMapTileTask`;
    const serviceAccountEmail = getTasksServiceAccountEmail(projectId);

      const pendingTaskPromises = [];
      const FLUSH_SIZE = 100;
      let tasksCreated = 0;
      let tilesScheduled = 0;

      async function flushPending(reason) {
        if (!pendingTaskPromises.length) {
          return;
        }
        const count = pendingTaskPromises.length;
        functions.logger.debug("Flushing task batch", {mapId, count, reason});
        await Promise.all(pendingTaskPromises);
        pendingTaskPromises.length = 0;
      }

    const TILES_PER_TASK = 500;

    for (const {id: chunkId, tiles} of sanitizedChunks) {
      functions.logger.debug("Processing chunk for task creation", {
        mapId,
        chunkId,
        tileCount: tiles.length
      });

      if (!tiles.length) {
        const payload = {mapId, chunkId, chunkSize, tiles: []};
        pendingTaskPromises.push(
          tasksClient.createTask({
            parent: queuePath,
            task: {
              httpRequest: {
                httpMethod: "POST",
                url: taskHandlerUrl,
                headers: {"Content-Type": "application/json"},
                body: Buffer.from(JSON.stringify(payload)).toString("base64"),
                oidcToken: {serviceAccountEmail}
              }
            }
          }).then(() => {
            tasksCreated += 1;
          })
        );
        await flushPending("emptyChunk");
        continue;
      }

      for (let start = 0; start < tiles.length; start += TILES_PER_TASK) {
        const slice = tiles.slice(start, start + TILES_PER_TASK);
        const payload = {
          mapId,
          chunkId,
          chunkSize,
          tiles: slice
        };

        const task = {
          parent: queuePath,
          task: {
            httpRequest: {
              httpMethod: "POST",
              url: taskHandlerUrl,
              headers: {
                "Content-Type": "application/json"
              },
              body: Buffer.from(JSON.stringify(payload)).toString("base64"),
              oidcToken: {
                serviceAccountEmail
              }
            }
          }
        };

        pendingTaskPromises.push(
          tasksClient.createTask(task).then(() => {
            tasksCreated += 1;
            tilesScheduled += slice.length;
          }).catch((err) => {
            functions.logger.error("Failed to create task", {
              mapId,
              chunkId,
              tileCount: slice.length,
              error: err.message
            });
            throw err;
          })
        );
        if (pendingTaskPromises.length >= FLUSH_SIZE) {
          await flushPending("flushSizeReached");
        }
      }
    }

      await flushPending("finalize");

    const shouldPerformDeletes = deleteMissingChunks && chunkIdsOverrideSet !== null;
    const chunkIds = Array.from(targetChunkIdSet);

    if (shouldPerformDeletes) {
      const deleteOperations = [];
      const existingChunkRefs = await chunksCollection.listDocuments();
      for (const ref of existingChunkRefs) {
        if (!targetChunkIdSet.has(ref.id)) {
          deleteOperations.push((batch) => batch.delete(ref));
        }
      }
      await commitOperationsInBatches(deleteOperations, "deleteChunks");
    }

    const mapDocument = {
      PlanetName: planetName || null,
      PlanetSurface: planetSurface || null,
      PlanetSize: planetSize,
      chunkSize,
      updatedAt: now
    };

    const mapDataDocument = {
      ChunkSize: chunkSize,
      updatedAt: now
    };

    const isFinalBatch = chunkIdsOverrideSet !== null;
    if (isFinalBatch) {
      mapDocument.tileCount = tileCount;
      mapDataDocument.ChunkIds = chunkIds;
      mapDataDocument.TileCount = tileCount;
    }

    await Promise.all([
      mapRef.set(mapDocument, {merge: true}),
      mapDataRef.set(mapDataDocument, {merge: true})
    ]);

      functions.logger.info(
        `Queued ${tilesScheduled} tiles across ${tasksCreated} tasks for map '${mapId}' (chunks this batch: ${sanitizedChunks.length}).`,
        {
          mapId,
          tasksCreated,
          tilesScheduled,
          chunkBatchSize: sanitizedChunks.length,
          isFinalBatch,
          chunkIdsCount: chunkIds.length
        }
      );

      return {
        success: true,
        mapId,
        tasksCreated,
        tilesScheduled,
        processedChunkIds: sanitizedChunks.map((chunk) => chunk.id),
        finalBatch: isFinalBatch,
        queue: queuePath
      };
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      functions.logger.error("saveMap failed", {
        mapId: data && typeof data === "object" ? data.mapId : undefined,
        message,
        stack: err instanceof Error ? err.stack : undefined
      });
      if (err instanceof functions.https.HttpsError) {
        throw err;
      }
      throw new functions.https.HttpsError("unknown", message || "saveMap failed");
    }
  });

const processMapTileTask = functions
  .region("us-west2")
  .runWith({timeoutSeconds: 120, memory: "8GB"})
  .https.onRequest(async (req, res) => {
    // Ensure Firestore is initialized
    ensureDb();

    if (req.method !== "POST") {
      res.status(405).send("Method Not Allowed");
      return;
    }

    const {mapId, chunkId, tiles, tile, chunkSize} = req.body || {};

    if (typeof mapId !== "string" || mapId.trim().length === 0) {
      res.status(400).json({error: "mapId must be provided."});
      return;
    }

    if (typeof chunkId !== "string" || chunkId.trim().length === 0) {
      res.status(400).json({error: "chunkId must be provided."});
      return;
    }

    const mapDataRef = firestore.collection("MapData").doc(mapId);
    const chunkDoc = mapDataRef.collection("Chunks").doc(chunkId);

    let tileArray = [];
    if (Array.isArray(tiles)) {
      tileArray = tiles;
    } else if (tile && typeof tile === "object") {
      tileArray = [tile];
    }

    const normalizedTiles = [];
    for (const entry of tileArray) {
      if (!entry || typeof entry !== "object") {
        continue;
      }

      const x = entry.x ?? entry.X;
      const y = entry.y ?? entry.Y;
      if (typeof x !== "number" || typeof y !== "number") {
        continue;
      }

      const tileLayerValue = entry.TileLayer ?? entry.tileLayer ?? "Surface";
      const tileNameValue = entry.TileName ?? entry.tileName ?? null;
      const tileObjectPathValue = entry.TileObjectPath ?? entry.tileObjectPath ?? null;
      const transformValue = entry.Transform ?? entry.transform ?? null;
      const normalizedTransform = Array.isArray(transformValue) && transformValue.length === 16
        ? transformValue.map((value) => Number(value))
        : null;

      normalizedTiles.push({
        x,
        y,
        TileLayer: tileLayerValue,
        TileName: tileNameValue,
        TileObjectPath: tileObjectPathValue,
        Transform: normalizedTransform
      });
    }

    const updateData = {
      Tiles: normalizedTiles.length ? normalizedTiles : [],
      updatedAt: FieldValue.serverTimestamp()
    };

    if (typeof chunkSize === "number" && chunkSize > 0) {
      updateData.chunkSize = chunkSize;
    }

    await chunkDoc.set(updateData, {merge: false});

    functions.logger.debug("processMapTileTask wrote tiles", {
      mapId,
      chunkId,
      processedTiles: normalizedTiles.length
    });

    res.status(200).json({success: true, processedTiles: normalizedTiles.length});
  });

const health = functions.https.onCall(async () => {
  return { status: "ok", time: admin.firestore.Timestamp.now().toDate().toISOString() };
});

module.exports = {
  saveMap,
  processMapTileTask,
  health
};
