using UnityEngine;
using System;

[Serializable]
public class PlayerInput : MonoBehaviour
{
    public Grid PlanetGrid;
    public int MapTouchDampering = 80;
    public int MapTouchSesitivity = 3;
    public float MapTouchEasingDistance = .5f;
    public float MapTouchEasingSpeed = .05f;
    private float MapTouchEasingCounter = 0f;
    void Start()
    {
        
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // if (touch.phase == TouchPhase.Began)
            // {
            //     // click tile

            //     Vector3 WorldPosition = Camera.main.ScreenToWorldPoint(Input.touches[0].position);
            //     Debug.Log(PlanetGrid.WorldToCell(WorldPosition));


            //     // RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.touches[0].position), Vector2.zero);
            //     // Debug.Log(hit.collider);
            //     // if(hit.collider) {
            //     //     Debug.Log(hit.collider);
            //     // } 
                
            // }
            // else if (touch.phase == TouchPhase.Moved)
            // {
            //     Camera.main.transform.position = Vector3.Lerp(
            //     Camera.main.transform.position, 
            //     new Vector3(
            //      Camera.main.transform.position.x - ( touch.deltaPosition.x / MapTouchDampering ), 
            //      Camera.main.transform.position.y - ( touch.deltaPosition.y / MapTouchDampering ), 
            //      Camera.main.transform.position.z
            //     ), 
            //     Time.deltaTime * MapTouchSesitivity);
            // }
            // get touch velocity added for quick multiplayer movements on the map.
            // fast teleporting in an enemy server is needed.
            // adding the scolling velocity will help player explore the map quicley when scouting or attacking.
            //
            // else if (touch.phase == TouchPhase.Ended)
            // {
            //     MapTouchEasingCounter = 0.01f;
            //     if (MapTouchEasingCounter != 0f && MapTouchEasingCounter <= 1f)
            //     {
            //         Debug.Log(MapTouchEasingCounter);
            //         Camera.main.transform.position = Vector3.Lerp(
            //         Camera.main.transform.position, 
            //         new Vector3(
            //         // the new positions MapTouchEasingDistance
            //         // x,
            //         // y,
            //         Camera.main.transform.position.z
            //         ), 
            //         MapTouchEasingCounter);
            //         MapTouchEasingCounter += (Time.deltaTime + .2f);
            //     }
            // }
            
        }
    }
}
