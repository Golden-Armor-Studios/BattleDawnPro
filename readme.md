# BattleDawnPro – Editor Operations Toolkit

BattleDawnPro’s Unity editor tooling centralises live-game configuration so designers can stay in sync with Firestore without leaving the editor. This plug-in bundles research/buff/attribute management, Firestore migrations, GitHub-based authentication, and the new Maps workflow in one place.


## Features

- **Integrated Firebase sign-in** – authenticate with a secure dev token and automatically attach the active user/avatar to the editor.
- **Game configuration editors** – manage Attributes, Buffs, and Researches with live Firestore reads/writes.
- **Maps management** – create, load, and persist world maps straight from the “Maps” foldout.
- **One-click Firestore migration** – seed the Firestore `battledawnpro` database with baseline data after connecting to `goldenarmorstudios`.
- **Persistent session state** – the tool stores your dev token securely (EditorPrefs) for the next login.

---

## Prerequisites

- Unity **2021.3 LTS** (or the project’s pinned version).  
- Firebase Unity SDK imported (Authentication + Firestore).  
- Access to the **GoldenArmorStudios** Firebase project.  
- Dev token generation via Golden Armor Studios’ Dev Tools portal.  
- Git installed (2.30+) with Git LFS extension.

---

## Git LFS Setup

Large binary assets (textures, audio, prefabs) are tracked with Git LFS. Run the following once per machine:

```bash
# install Git LFS for your platform if you haven't
git lfs install

```

> **Reminder:** after cloning the repository, run `git lfs pull` to ensure all large files download correctly.

---

## Unity Plug-in Walkthrough

Open the tooling via **Game Config → RunTime GameConfig** in the Unity editor menu. The window is organised top-to-bottom to mirror the workflow:

### 1. Log In with a Dev Token

1. Click `Login` in the Firebase foldout.  
2. Follow the inline steps:
   - Request a token from [the Dev Tools page](https://goldenarmorstudio.art/dev-tools).  
   - If you’re not on the developer roster, apply via [the signup form](https://goldenarmorstudio.art/join-team).  
   - Need help? Join the [community Discord](https://goldenarmorstudio.art/community).  
3. Paste the token and click `Login`. On success the status updates to `Signed in as ... · DB: battledawnpro` and your GitHub avatar/name appear in the header. Tokens are cached in `EditorPrefs`.

### 2. Review Firebase Status

- `Sign Out` clears the local token and resets cached UI state.  
- `Migrate Firestore Data` seeds baseline collections/documents (see [Firestore Migration](#firestore-migration)).

### 3. Manage Game Data

- **Attributes / Buffs / Researches** foldouts load live data once a session after sign-in. Each foldout offers add/edit/delete experiences backed by Firestore.
- Real-time refreshes occur when:
  - You modify data inside the toolkit.  
  - Firestore snapshots push updates (current session only).

### 4. Maps Foldout

See [Maps Workflow](#maps-workflow) for deeper instructions.

---

## Firebase / Auth Configuration

- Client configuration points to Firebase **project `goldenarmorstudios`** the Firestore database path defaults to `battledawnpro` (secondary database).
- Custom tokens **must** be minted by the same Firebase project (`goldenarmorstudios`). If you see “token corresponds to a different audience,” reissue the token using a service account from the correct project.
- The login helper stores the dev token under `PlayerPrefs` key `GoldenArmorSudioDevAuthToken`. Clearing this key signs you out.

---

## Maps Workflow

The Maps foldout replaces the old `MapController` window. All interactions stay inside the Game Config panel:

1. **Create Map** – enter a planet name, adjust the width (100–1000 tiles), then click `Create Map`. A default ocean surface is saved along with an empty tile dataset.
2. **Live List** – once signed in, the toolkit listens to `Maps` and `MapData`. Each map entry shows ID, name, and size.
3. **Load Map** – spawns the grid, surface tilemap, and previously cleared ground tiles into the current scene.
4. **Save Map** – writes the tilemap back to Firestore (`Maps` + `MapData`).
5. **Delete** – removes both the metadata and tile data documents.

Status messages appear under the controls (e.g., missing name, auth required, success/failure messages).

---

## Firestore Migration

After logging in:

1. Click `Migrate Firestore Data`.  
2. The tool reinitialises Firestore for `battledawnpro`, ensures required collections exist, and seeds:
   - `gameConfig/AttributeConfig` document metadata.  
   - Starter Attributes, Buffs, and Researches (IDs prefixed with `attack-damage`, `buff-orbital-salvo`, etc.).
3. On success the toolkit reloads all foldouts.

> Re-run the migration whenever you set up a fresh Firestore instance or reset data for QA environments.

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| `Firebase custom token login failed: The custom token corresponds to a different audience` | Ensure the token is generated by the `goldenarmorstudios` project and the Unity config (JSON/plist) matches. |
| Foldouts show “Sign in to load …” after login | Use `Migrate Firestore Data` once; if it persists, confirm Firestore rules allow the authenticated user. |
| Maps foldout empty | Confirm you are signed in and that the Firestore `Maps` collection has documents; check the Console for listener errors. |
| Git push rejects large files | Run `git lfs install` and ensure `.gitattributes` contains rules before committing big binaries. |

---

## Contributing

1. Branch from `main` (`git checkout -b feat/your-feature`).  
2. Keep commits focused and run `dotnet build Assembly-CSharp.csproj` to ensure the editor scripts compile.  
3. Validate Firestore interactions against a staging environment whenever possible.  
4. Open a PR with a concise summary, screenshots (if UI changes), and testing notes.

Thanks for building BattleDawnPro with us! Together we keep the live ops pipeline tight, safe, and fast. If you have questions, ping the team in Discord or reach out via the developer portal.
