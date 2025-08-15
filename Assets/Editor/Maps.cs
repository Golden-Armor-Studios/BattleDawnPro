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

public class Maps : EditorWindow
{

    [MenuItem("Game Config/Maps")]
    static void Init()
    {
        Maps wnd = GetWindow<Maps>();
        wnd.titleContent = new GUIContent("Maps");
        wnd.Show();

    }

    void OnEnable()
    {

    }

    // void OnGUI() {
    //     using ( var Login = new EditorGUILayout.FadeGroupScope(ShowLogin.faded) )
    //     {
    //         if (Login.visible)
    //         {   
    //             EditorGUILayout.Separator();
    //             GUILayout.Label("Please log using the AWS credentials provided to you. Once logged in, this allows you to authorize all your api calls in unity to our backend server and allows you to test the game as we build it.", EditorStyles.wordWrappedLabel);
    //             EditorGUILayout.Separator();
    //             EditorGUILayout.PrefixLabel("Username");
    //             username = EditorGUILayout.TextField(username);
    //             EditorGUILayout.PrefixLabel("Password");
    //             password = EditorGUILayout.TextField(password);
    //             EditorGUILayout.Separator();
    //             if (GUILayout.Button("Login"))
    //             {
    //                 LoginRequest();
    //             }
    //         }
    //     }

    // }

}