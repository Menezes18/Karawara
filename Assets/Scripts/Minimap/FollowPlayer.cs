using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour {
    public bool rotate = false;
    Transform player;

    void Start() {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // Update is called once per frame
    void Update() {
        // Vector3 pos = player.position;
        // pos.y = transform.position.y;
        // transform.position = pos;
        //
        // if (rotate) {
        //     // Transform player Y rotation into camera Z rotation
        //     transform.rotation = Quaternion.Euler(89, player.eulerAngles.y, 0);
        // }
    }
}
