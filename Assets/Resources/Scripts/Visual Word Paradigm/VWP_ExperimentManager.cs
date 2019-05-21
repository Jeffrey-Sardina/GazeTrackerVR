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
public class VWP_ExperimentManager : MonoBehaviour
{
    GameObject fixationDot,
        imagePlane;
    readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 1.5f, 10);
    readonly int FIXATION_DOT_DISPLAY_TIME = 1000,
        IMAGE_DISPLAY_TIME = 1000;
    float fixationDotDisplayStartTime;
    bool displayingFixationDot,
        displayingImage,
        playingSound;

    /// <summary>
    /// 
    /// </summary>
    void Start()
    {
        DisplayFixationDot(DISPLAY_LOCATION);
        fixationDotDisplayStartTime = Time.time;
        displayingFixationDot = true;
    }

    void DisplayFixationDot(Vector3 location)
    {
        //Place the fixation dot
        fixationDot = Instantiate(Resources.Load("Visual Word Paradigm/FixationDot") as GameObject);
        fixationDot.transform.position = location;
    }

    GameObject DisplayImage(string imageFileName, Vector3 location)
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject imagePlane = Instantiate(Resources.Load("ImagePlanePrefab") as GameObject);
        imagePlane.transform.position = location;

        //Load the image onto the plane prefab
        Texture2D image_texture = Resources.Load("Visual Word Paradigm/stimuli/images/" + imageFileName) as Texture2D;
        Material image_material = imagePlane.GetComponent<Material>();
        image_material.mainTexture = image_texture;

        //Return the instantiated GameObject
        return imagePlane;
    }

    /// <summary>
    /// 
    /// </summary>
    void Update()
    {

    }
}
