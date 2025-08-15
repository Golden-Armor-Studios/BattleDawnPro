using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.AnimatedValues;
using UnityEngine.Events;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System;

public class AWSLogin : EditorWindow
{
    private string username;
    private string password;
    private string mfa;
    private string confirmPassword;
    private bool IsLoggedIn;
    AnimBool ShowMFAScreen;
    AnimBool ShowLogin;
    AnimBool ShowChangePassword;
    AnimBool ShowLoggedIn;

    public AWSLoginResult AWSSession;

    [MenuItem("Game Config/AWS Login")]
    static void Init()
    {
        AWSLogin wnd = GetWindow<AWSLogin>();
        wnd.titleContent = new GUIContent("AWS Login");
        wnd.Show();

    }

    void OnEnable()
    {
        if(AWSSession != null) {
            ShowLogin = new AnimBool(true);
            ShowLogin.valueChanged.AddListener(new UnityAction(Repaint));

            ShowMFAScreen = new AnimBool(false);
            ShowMFAScreen.valueChanged.AddListener(new UnityAction(Repaint));

            ShowChangePassword = new AnimBool(false);
            ShowChangePassword.valueChanged.AddListener(new UnityAction(Repaint));

            ShowLoggedIn = new AnimBool(false);
            ShowLoggedIn.valueChanged.AddListener(new UnityAction(Repaint));
        }
    }

    void OnGUI() {
        using ( var Login = new EditorGUILayout.FadeGroupScope(ShowLogin.faded) )
        {
            if (Login.visible)
            {   
                EditorGUILayout.Separator();
                GUILayout.Label("Please log using the AWS credentials provided to you. Once logged in, this allows you to authorize all your api calls in unity to our backend server and allows you to test the game as we build it.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Separator();
                EditorGUILayout.PrefixLabel("Username");
                username = EditorGUILayout.TextField(username);
                EditorGUILayout.PrefixLabel("Password");
                password = EditorGUILayout.TextField(password);
                EditorGUILayout.Separator();
                if (GUILayout.Button("Login"))
                {
                    LoginRequest();
                }
            }
        }

        using ( var MFA = new EditorGUILayout.FadeGroupScope(ShowMFAScreen.faded) )
        {
            if (MFA.visible)
            {
                EditorGUILayout.PrefixLabel("Enter MFA Code");
                mfa = EditorGUILayout.TextField(mfa);
                EditorGUILayout.Separator();
                if (GUILayout.Button("Enter MFA"))
                {
                    Debug.Log("Clicked Button");
                }
            }
        }

        using ( var ChangePassword = new EditorGUILayout.FadeGroupScope(ShowChangePassword.faded) )
        {
            if (ChangePassword.visible)
            {
                EditorGUILayout.Separator();
                GUILayout.Label("Your admin has changed your password. Please set a new password.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Separator();
                EditorGUILayout.PrefixLabel("Enter a new password");
                password = EditorGUILayout.TextField(password);
                EditorGUILayout.Separator();
                if (GUILayout.Button("Change Password"))
                {
                    ChangePasswordRequest();
                }
            }
        }

        using ( var LoggedIn = new EditorGUILayout.FadeGroupScope(ShowLoggedIn.faded) )
        {
            if (LoggedIn.visible)
            {
                EditorGUILayout.Separator();
                GUILayout.Label("Logged In: \n"+username, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Separator();
                if (GUILayout.Button("Sign Out"))
                {
                    SignOutRequest();
                }
            }
        }
    }

    public void LoginRequest()
    {
        AWSUserLoginRequest request = new AWSUserLoginRequest();
        request.username = username;
        request.password = password;
        request.LoginType = "Login";
        using (UnityWebRequest www = UnityWebRequest.Post("https://game-api.compassoftruth.org/prod/Login", JsonUtility.ToJson(request), "application/json") )
        {
            www.SendWebRequest();
            while (www.isDone == false) {
            //do something, or nothing while blocking
            }
            if (www.result != UnityWebRequest.Result.Success) {
                Debug.Log(www.error);
            }
            else {
                AWSSession = JsonUtility.FromJson<AWSLoginResult>(www.downloadHandler.text.Replace('$'.ToString(), ""));
                switch(AWSSession.ChallengeName){
                    case "NEW_PASSWORD_REQUIRED":
                        ShowLogin = new AnimBool(false);
                        ShowChangePassword = new AnimBool(true);
                        break;
                    default : {
                        if (AWSSession.AuthenticationResult.TokenType == "Bearer") {
                            EditorPrefs.SetString("AWS_IDTOKEN",AWSSession.AuthenticationResult.IdToken);
                            ShowLogin = new AnimBool(false);
                            ShowChangePassword = new AnimBool(false);
                            ShowLoggedIn = new AnimBool(true);
                        }
                        return;
                    };
                }
            }
        }
    }

    public void ChangePasswordRequest()
    {
        AWSUserLoginRequest request = new AWSUserLoginRequest();
        request.username = username;
        request.password = password;
        request.Session = AWSSession.Session;
        request.LoginType = "NEW_PASSWORD_REQUIRED";
        using (UnityWebRequest www = UnityWebRequest.Post("https://game-api.compassoftruth.org/prod/Login", JsonUtility.ToJson(request), "application/json") )
        {
            www.SendWebRequest();
            while (www.isDone == false) {
            //do something, or nothing while blocking
            }
            if (www.result != UnityWebRequest.Result.Success) {
                ShowLogin = new AnimBool(true);
                ShowChangePassword = new AnimBool(false);
                Debug.Log(www.error);
            }
            else {
                AWSSession = JsonUtility.FromJson<AWSLoginResult>(www.downloadHandler.text.Replace('$'.ToString(), ""));
                Debug.Log(www.downloadHandler.text.Replace('$'.ToString(), ""));
                switch(AWSSession.AuthenticationResult.TokenType){
                    case "Bearer":
                        EditorPrefs.SetString("AWS_IDTOKEN",AWSSession.AuthenticationResult.IdToken);
                        ShowLogin = new AnimBool(false);
                        ShowChangePassword = new AnimBool(false);
                        ShowLoggedIn = new AnimBool(true);
                        break;
                    default : return;
                }
            }
        }
    }

    public void SignOutRequest()
    {
        EditorPrefs.SetString("AWS_IDTOKEN", "");
        AWSUserLoginRequest request = new AWSUserLoginRequest();
        request.username = username;
        request.LoginType = "SignOut";
        using (UnityWebRequest www = UnityWebRequest.Post("https://game-api.compassoftruth.org/prod/Login", JsonUtility.ToJson(request), "application/json") )
        {
            www.SendWebRequest();
            while (www.isDone == false) {
            //do something, or nothing while blocking
            }
            if (www.result != UnityWebRequest.Result.Success) {
                Debug.Log(www.error);
            }
            else {
                ShowLogin = new AnimBool(true);
                ShowChangePassword = new AnimBool(false);
                ShowLoggedIn = new AnimBool(false);
            }
        }
    }
}