using System;
using Firebase;
using System.Reflection;
using Firebase.Firestore;
using Auth;
using UnityEngine;
using Random = System.Random;

namespace DB
{
    public static class Default 
    {
        private const string PUSH_CHARS = "-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
        private static readonly Random random = new Random();
        private static long lastPushTime = 0L;
        private static char[] lastRandChars = new char[8];
        private static FirebaseApp app;
        private const string DefaultDatabaseName = "battledawnpro";
        private static FirebaseFirestore db;
        public static CollectionReference gameConfig;
        public static CollectionReference maps;
        public static CollectionReference MapData;
        public static CollectionReference GameProfiles;
        public static string DatabaseName { get; private set; } = DefaultDatabaseName;
        public static FirebaseFirestore Database => db;

        public static void Init (string databaseName = null) {
            app = Auth.User.app;
            if (app == null) {
                Debug.LogWarning("Firebase app is not initialised; cannot configure Firestore.");
                return;
            }

            DatabaseName = string.IsNullOrEmpty(databaseName) ? DefaultDatabaseName : databaseName;
            db = GetFirestoreInstance(app, DatabaseName);
            if (db == null) {
                Debug.LogError("Failed to obtain a Firestore instance.");
                return;
            }
            gameConfig = db.Collection("gameConfig");
            maps = db.Collection("Maps");
            MapData = db.Collection("MapData");
            GameProfiles = db.Collection("Users").Document("Profiles").Collection("GameProfiles");
        }

        public static string GenerateId(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentException("Length must be a positive number.");
            }

            // Generate the timestamp component (48 bits)
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string timestampPart = Encode(timestamp, 8); // 48 bits = 6 bytes

            // Truncate the random component to meet the desired length
            int randomPartLength = length - timestampPart.Length;
            if (randomPartLength <= 0)
            {
                return timestampPart.Substring(0, length);
            }
            
            // Generate the random component (72 bits in standard Firebase, we'll use fewer)
            string randomPart = EncodeRandom(randomPartLength);

            return timestampPart + randomPart;
        }

        // Encodes a long into a Base64-like string
        private static string Encode(long value, int outputLength)
        {
            var buffer = new char[outputLength];
            for (int i = outputLength - 1; i >= 0; i--)
            {
                buffer[i] = PUSH_CHARS[(int)(value % 64)];
                value = value >> 6;
            }
            return new string(buffer);
        }

        // Generates a random string using the custom character set
        private static string EncodeRandom(int length)
        {
            var random = new Random();
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = PUSH_CHARS[random.Next(64)];
            }
            return new string(buffer);
        }

        private static FirebaseFirestore GetFirestoreInstance(FirebaseApp firebaseApp, string databaseName)
        {
            FirebaseFirestore instance = null;
            try {
                if (!string.IsNullOrEmpty(databaseName))
                {
                    MethodInfo overload = typeof(FirebaseFirestore).GetMethod(
                        "GetInstance",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(FirebaseApp), typeof(string) },
                        null);

                    if (overload != null)
                    {
                        instance = overload.Invoke(null, new object[] { firebaseApp, databaseName }) as FirebaseFirestore;
                    }
                }
            } catch (TargetInvocationException ex) {
                Debug.LogWarning($"Unable to select Firestore database '{databaseName}': {ex.InnerException?.Message ?? ex.Message}");
                instance = null;
            } catch (Exception ex) {
                Debug.LogWarning($"Unable to select Firestore database '{databaseName}': {ex.Message}");
                instance = null;
            }

            if (instance == null)
            {
                instance = FirebaseFirestore.GetInstance(firebaseApp);
            }

            return instance;
        }

    }

}
