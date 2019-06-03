using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class manages the Unity Scene for a Visual Word Paradigm experiment. There are three main phases in this experiment paradigm:
///     Callibration
///         A fixation dot is shown for 1000ms and the subject is asked to look at it.
///     Data Collection
///         A visual stimulus is shown for 1000ms
///         An audio recording is played. 2000ms after the audio recording finishes, the scene transfers to the next stimulus.
///     Termination
///         Once all stimuli have run, the program write out the collected data and resets for the next subject.
/// </summary>
public class VCP_ExperimentManager : MonoBehaviour
{
    readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 1.5f, 10);

    void Start()
    {
        //Place the fixation dot
        //GameObject fixationDot = Instantiate(Resources.Load("Visual Word Paradigm/FixationDot") as GameObject);
        //fixationDot.transform.position = DISPLAY_LOCATION;
    }
}
