using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.Video;

/// <summary>
/// This class manages the Unity Scene for a Visual Word Paradigm experiment. There are three main phases in this experiment paradigm:
///     Callibration
///         A fixation dot is shown for 1000ms and the subject is asked to look at it.
///     Data Collection
///         A visual stimulus is shown for 1000ms
///         A video recording is played. 2000ms after the video recording finishes, the scene transfers to the next stimulus.
///     Termination
///         Once all stimuli have run, the program write out the collected data and resets for the next subject.
/// </summary>
public class test_CameraController : MonoBehaviour
{
    public void Start()
    {

    }

    public void Update()
    {
        if (Input.anyKeyDown)
            Debug.Log(Input.inputString);
    }
}