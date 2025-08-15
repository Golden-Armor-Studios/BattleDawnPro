using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;
using System.Collections;

[Serializable]
public class AWSUserLoginRequest
{
    public string username;
    public string password;
    public string LoginType;
    public string Session;
}
