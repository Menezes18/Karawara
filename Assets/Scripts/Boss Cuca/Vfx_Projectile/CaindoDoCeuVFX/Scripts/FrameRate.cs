using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrameRate : MonoBehaviour
{
    public int frameRate = 165;

    void Start()
    {
        Application.targetFrameRate = frameRate;
    }
}
