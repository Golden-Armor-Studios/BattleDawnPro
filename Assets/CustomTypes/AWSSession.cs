using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using Unity.VisualScripting;

[Serializable]
public class AWSLoginResult
{
    public metadata metadata;
    public string ChallengeName;
    public ChallangeParameters ChallangeParameters;
    public string Session;

    public AuthenticationResult AuthenticationResult;

}

[Serializable]
public class metadata
{
    public string httpStatusCode;
    public string requestId;
    public string attempts;
    public string totalRetryDelay;

}

[Serializable]
public class ChallangeParameters
{
    public string USER_ID_FOR_SRP;
    public string userAttributes;
    public string requiredAttributes;

}

[Serializable]
public class AuthenticationResult 
{
    public string AccessToken;
    public string ExpiresIn;
    public string IdToken;
    public string RefreshToken;
    public string TokenType;
}
