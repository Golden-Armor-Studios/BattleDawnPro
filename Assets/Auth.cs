using System;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.UIElements;
using Game;
using UI;
using System.Threading.Tasks;

namespace Auth
{
    public static class User  {
        public static FirebaseApp app;
        public static FirebaseAuth auth;
        public static FirebaseUser user;
        private static Profile Profile;
        private static DependencyStatus dependencyStatus = DependencyStatus.UnavailableOther;
        private static System.Threading.Tasks.Task initializationTask;
        private static bool isInitializing;
        private static bool isPostLoginInitialized;
        private const string FirebaseAuthTokenPrefKey = "GoldenArmorSudioDevAuthToken";

        public static void Init () {
            if (initializationTask != null && !initializationTask.IsFaulted && !initializationTask.IsCanceled) {
                return;
            }
            isInitializing = true;
            initializationTask = InitializeFirebaseAsync();
        }

        public static async System.Threading.Tasks.Task<FirebaseUser> EnsureLoggedInAsync()
        {
            if (initializationTask == null) {
                Init();
            }

            if (initializationTask != null) {
                await initializationTask;
            }

            if (auth == null) {
                return null;
            }

            if (auth.CurrentUser == null) {
                if (PlayerPrefs.HasKey(FirebaseAuthTokenPrefKey)) {
                    string storedToken = PlayerPrefs.GetString(FirebaseAuthTokenPrefKey);
                    if (!string.IsNullOrEmpty(storedToken)) {
                        await LoginWithGitHubAsync(storedToken, persistToken: false);
                    }
                }
            } else {
                user = auth.CurrentUser;
                await PostLoginSetupAsync();
            }

            return user;
        }

        private static async System.Threading.Tasks.Task InitializeFirebaseAsync()
        {
            try {
                try {
                    dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                    if (dependencyStatus != DependencyStatus.Available) {
                        Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                        return;
                    }
                } catch (Exception ex) {
                    Debug.LogError($"Failed checking Firebase dependencies: {ex.Message}");
                    return;
                }

                app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;
                auth.StateChanged += AuthStateChanged;
                Debug.Log("Firebase initialized successfully.");
                if (auth.CurrentUser != null) {
                    user = auth.CurrentUser;
                    await PostLoginSetupAsync();
                }
            } finally {
                isInitializing = false;
            }
        }

        private static async System.Threading.Tasks.Task PostLoginSetupAsync()
        {
            if (user == null || isPostLoginInitialized) {
                return;
            }

            try {
                DB.Default.Init();
                ResearchUI.InitUI(); // edit security rules for accessing research and buffs in the ui from the game config.
                DocumentReference secureProfileDocumentReference = DB.Default.GameProfiles.Document(user.UserId);
                DocumentSnapshot secureProfileDocument = await secureProfileDocumentReference.GetSnapshotAsync();
                if (secureProfileDocument.Exists) {
                    Profile = secureProfileDocument.ConvertTo<Profile>();
                } else {
                    Profile = new Profile();
                }
                await secureProfileDocumentReference.SetAsync(Profile, SetOptions.MergeAll);
                isPostLoginInitialized = true;
            } catch (FirebaseException e) {
                Debug.LogError($"Firestore profile sync failed: {e.Message}");
            }
        }

        public static async System.Threading.Tasks.Task<FirebaseUser> LoginWithGitHubAsync(string firebaseAuthToken, bool persistToken = true)
        {
            if (auth == null) {
                await InitializeFirebaseAsync();
            }
            if (auth == null) {
                return null;
            }
            if (string.IsNullOrEmpty(firebaseAuthToken)) {
                Debug.LogWarning("Firebase authentication token is empty.");
                return null;
            }

            try {
                Firebase.Auth.AuthResult authResult = await auth.SignInWithCustomTokenAsync(firebaseAuthToken);
                FirebaseUser firebaseUser = authResult?.User;
                user = firebaseUser ?? auth.CurrentUser;
                if (user == null) {
                    Debug.LogError("Firebase custom token sign-in completed without returning a user.");
                    return null;
                }
                if (persistToken) {
                    PlayerPrefs.SetString(FirebaseAuthTokenPrefKey, firebaseAuthToken);
                    PlayerPrefs.Save();
                }
                await PostLoginSetupAsync();
                return user;
            } catch (FirebaseException e) {
                Debug.LogError($"Firebase custom token login failed: {e.ErrorCode} - {e.Message}");
                if (persistToken && PlayerPrefs.HasKey(FirebaseAuthTokenPrefKey)) {
                    PlayerPrefs.DeleteKey(FirebaseAuthTokenPrefKey);
                }
            } catch (Exception ex) {
                Debug.LogError($"Firebase custom token login failed: {ex.Message}");
            }
            return null;
        }

        public static void Logout()
        {
            if (auth == null) {
                return;
            }

            try {
                auth.SignOut();
            } catch (Exception ex) {
                Debug.LogError($"Error during Firebase sign out: {ex.Message}");
            }

            if (PlayerPrefs.HasKey(FirebaseAuthTokenPrefKey)) {
                PlayerPrefs.DeleteKey(FirebaseAuthTokenPrefKey);
            }

            user = null;
            isPostLoginInitialized = false;
        }

        private static void AuthStateChanged (object sender, System.EventArgs eventArgs) {
            if (auth == null) {
                return;
            }

            user = auth.CurrentUser;
            if (user == null) {
                isPostLoginInitialized = false;
                _ = EnsureLoggedInAsync();
            } else {
                _ = PostLoginSetupAsync();
            }
        }
    }

}
