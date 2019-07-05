using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.Video;

/// <summary>
/// There are two main phases in this experiment paradigm:
///     Callibration
///         A fixation dot is shown for and the subject is asked to look at it for a time
///     Data Collection
///         A visual stimulus is shown for
///         A video recording is played. A short time after the video recording finishes, the scene transfers to the next stimulus.
/// Author: Siothrún (Jeffrey) Sardina
/// </summary>
public class VCP_CameraController : MonoBehaviour
{
    #region instance data
    //Camera data
    Camera vrCam;

    //Output
    StreamWriter writer;
    LinkedList<string> frameDataList = new LinkedList<string>();

    //Experiment constants
    const string PARADIGM_PATH = "Video-Corner Paradigm/",
        DATA_EXTENSION = ".csv",
        EXPERIMENT_FILE_FULL_NAME = "GT_",
        FIXATION_DOT = "FixationDot",
        READY_DOT = "ReadyDot";
    const int FIXATION_DOT_DISPLAY_TIME = 1,
        READY_DOT_DISPLAY_TIME = 1,
        IMAGE_INITIAL_DISPLAY_TIME_PRE_READY_DOT = 3,
        POST_VIDEO_KEEP_DISPLAYING_IMAGES_TIME = 5,
        RESETTING_DISPLAY_TIME = 2,
        NUM_TRAINING_STIMULI = 2,
        IMAGE_PLANE_SIDE_LENGTH = 3,
        FRAME_DATA_BUFFER_SIZE = 10;
    const float IMAGE_SCALE = 3f;
    readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 0, 10),
        QI = new Vector3(9, 7, 0),
        QII = new Vector3(-9, 7, 0),
        QIV = new Vector3(9, -7, 0),
        QIII = new Vector3(-9, -7, 0);

    //Experiment Management
    readonly List<string[]> STIMULUS_LIST = new List<string[]>();
    readonly List<string> VIDEO_LIST = new List<string>();
    readonly LinkedList<GameObject> CURRENT_STIMULI = new LinkedList<GameObject>();
    string experimentFileFullName,
        path,
        experimentSpecificationsFileName,
        videoName = "",
        q1ImageName = "",
        q2ImageName = "",
        q3ImageName = "",
        q4ImageName = "",
        currentEvent = "",
        stimuliNames = ",,,,";
    GameObject fixationDot,
        readyDot,
        thankYou,
        text,
        videoPlane;
    VideoPlayer videoPlayer;
    float fixationDotEarliestContinuousHitTime = -1,
        readyDotEarliestContinuousHitTime = -1,
        imageDisplayStartTime,
        videoCompletionTime,
        resettingStartTime,
        lastEventTriggerTime;
    bool fixationDotCallibrationDone,
        readyDotCallibrationDone,
        imageInitialDispayDone,
        videoDisplayDone,
        videoPlayingDone,
        resetting,
        trainingOver,
        startUpOver;
    int currentStimuliListIndex = -1;
    #endregion //instance data

    #region initialization functions
    /// <summary>
    /// This function is called once Unity has set up the scene.
    /// It initializes everythnig required for the experiment, but does not beginf the experiment
    ///     The experiment begins once Unity (separately) called Upadte()
    /// </summary>
    void Start()
    {
        ConfigureFileData();
        InitOutputStream();
        InitStimuliList();

        //Initialize Camera and data collection
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;

        //Display the background and fixation dot to get started
        DisplayBackgroundPlane(DISPLAY_LOCATION + new Vector3(0, 0, .01f)); //Offset a tad
        currentEvent = "Beginning training";
    }

    /// <summary>
    /// Sets up the output path and determines the names of output files for this experiment.
    /// Should be called before InitOutputStream
    /// </summary>
    void ConfigureFileData()
    {
        path = Application.persistentDataPath + "/";
        experimentFileFullName = EXPERIMENT_FILE_FULL_NAME + getCleanDateTime() + DATA_EXTENSION;
        experimentSpecificationsFileName = "EyeTrackerVRData_0";
    }

    /// <summary>
    /// Sets up the output stream and writes header data
    /// Precondition: ConfigureFileData has been called
    /// </summary>
    void InitOutputStream()
    {
        try
        {
            writer = new StreamWriter(path + experimentFileFullName);
            writer.WriteLine("QI," + ExactPoint(DISPLAY_LOCATION + QI));
            writer.WriteLine("QII," + ExactPoint(DISPLAY_LOCATION + QII));
            writer.WriteLine("QIII," + ExactPoint(DISPLAY_LOCATION + QIII));
            writer.WriteLine("QIV," + ExactPoint(DISPLAY_LOCATION + QIV));
            RecordData("QI image,QII image,QIII image,QIV image,video,stimulus ID,event,time,timeSinceLastEvent,x,y,z", true);
        }
        catch (Exception ex)
        {
            Debug.Log("CameraController: WriteData: Failed to write data file!");
            Debug.Log(ex.StackTrace);
        }
    }

    /// <summary>
    /// Initializes the list of stimuli to display. Stimuli are split into two parts:
    ///     Training stimuli
    ///     Experimental stimuli
    /// Training stimuli are always the same for all subjects (the first NUM_TRAINING_STIMULI entries in the experiment file)
    /// Experiment stimuli vary betweeen subjects
    /// The location of images for each round are also randomized.
    /// </summary>
    void InitStimuliList()
    {
        List<string[]> trainingList = new List<string[]>();
        TextAsset data = Resources.Load<TextAsset>(PARADIGM_PATH + "experimental_data/" + experimentSpecificationsFileName);
        string data_text = data.text;
        foreach (string line in data_text.Split('\n'))
        {
            //Split line data by entry
            string[] stimuliSet = line.Split(',');

            //Get the images
            int numNonImages = 2;
            string[] images = new string[stimuliSet.Length - numNonImages];
            for (int i = 0; i < stimuliSet.Length - numNonImages; i++)
            {
                images[i] = stimuliSet[i];
            }

            //Randomly shuffle the images
            ShuffleArray<string>(images);

            //Put the new order into the stimuliSet
            for (int i = 0; i < images.Length && i < stimuliSet.Length; i++)
            {
                stimuliSet[i] = images[i];
            }

            //Add the shuffled data to the STIMULUS_LIST
            STIMULUS_LIST.Add(stimuliSet);
        }

        //Load the first NUM_TRAINING_STIMULI elements from the simulus list as training elements only
        for (int i = 0; i < NUM_TRAINING_STIMULI; i++)
        {
            trainingList.Insert(0, STIMULUS_LIST[0]);
            STIMULUS_LIST.RemoveAt(0);
        }

        //Shuffle the STIMULUS_LIST, but not the training elements
        ShuffleList(STIMULUS_LIST);

        //Re-insert the training elements to the front of the STIMULUS_LIST
        for (int i = 0; i < NUM_TRAINING_STIMULI; i++)
        {
            STIMULUS_LIST.Insert(0, trainingList[0]);
            trainingList.RemoveAt(0);
        }
    }

    /// <summary>
    /// `Randomizes the given array using the Fisher-Yates algorithm. Reference for the algorithm here:
    ///     https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    void ShuffleArray<T>(T[] array)
    {
        System.Random gen = new System.Random();
        for (int i = 0; i < array.Length - 2; i++)
        {
            int randomIndex = gen.Next(0, array.Length);
            T temp = array[i];
            array[i] = array[randomIndex];
            array[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Randomized the given list using the Fisher-Yates algorithm. Reference for the algorithm here:
    ///     https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
    /// </summary>
    /// <param name="array"></param>
    void ShuffleList<T>(List<T> list)
    {
        System.Random gen = new System.Random();
        for (int i = 0; i < list.Count - 2; i++)
        {
            int randomIndex = gen.Next(0, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    #endregion //initialization functions

    #region display functions
    /// <summary>
    /// Displays a fixation dot. This function places the dot GameObject in 3D space and returns that GameObject.
    /// </summary>
    /// <param name="location">The location to display the dot</param>
    /// <param name="name">The name of the dot</param>
    /// <returns></returns>
    GameObject DisplayFixationDot(Vector3 location, string name)
    {
        //Place the fixation dot
        GameObject dot = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + FIXATION_DOT));
        dot.name = name;
        dot.transform.position = location;
        return dot;
    }

    /// <summary>
    /// Displays a background plane so that user gaze movement can be tracked in its motion accross the plane.
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    GameObject DisplayBackgroundPlane(Vector3 location)
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject backgroudPlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "BackgroundPlanePrefab"));
        backgroudPlane.transform.position = location;
        backgroudPlane.name = "backgroundPlane";
        return backgroudPlane;
    }

    /// <summary>
    /// Displays a single image in 3D space.
    /// </summary>
    /// <param name="imageFileName">The name of the image to display</param>
    /// <param name="location">Where to display the image</param>
    /// <param name="scale">The factor by which to scale the iamge</param>
    /// <param name="path">The path to the folder in which the image is. Defaults to stimuli/images/</param>
    /// <returns></returns>
    GameObject DisplayImage(string imageFileName, Vector3 location, float scale, string path = PARADIGM_PATH + "stimuli/images/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject imagePlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "ImagePlanePrefab"));
        imagePlane.transform.position = location;
        imagePlane.name = imageFileName;

        //Load the image onto the plane prefab
        Material image_material = new Material(Shader.Find("Diffuse"));
        Texture2D image_texture = Resources.Load<Texture2D>(path + imageFileName);

        Vector2 unitdim = new Vector2(image_texture.width, image_texture.height).normalized;
        imagePlane.transform.localScale = new Vector3(imagePlane.transform.localScale.x * unitdim.x * scale, imagePlane.transform.localScale.y * 1, imagePlane.transform.localScale.z * unitdim.y * scale);

        image_material.mainTexture = image_texture;
        imagePlane.GetComponent<Renderer>().material = image_material;

        RecordData("Scaled size of " + imageFileName + ": (" + (unitdim.x * scale * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * scale * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return imagePlane;
    }

    /// <summary>
    /// Displays all 4 stimuli in the corners of the vidual field.
    /// </summary>
    void DisplayStimuli()
    {
        Vector3 Q1 = DISPLAY_LOCATION + QI;
        Vector3 Q2 = DISPLAY_LOCATION + QII;
        Vector3 Q4 = DISPLAY_LOCATION + QIV;
        Vector3 Q3 = DISPLAY_LOCATION + QIII;

        q1ImageName = STIMULUS_LIST[currentStimuliListIndex][0];
        q2ImageName = STIMULUS_LIST[currentStimuliListIndex][1];
        q3ImageName = STIMULUS_LIST[currentStimuliListIndex][2];
        q4ImageName = STIMULUS_LIST[currentStimuliListIndex][3];

        CURRENT_STIMULI.AddFirst(DisplayImage(q1ImageName, Q1, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q2ImageName, Q2, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q3ImageName, Q3, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q4ImageName, Q4, IMAGE_SCALE));
        imageDisplayStartTime = Time.time;
    }

    /// <summary>
    /// Displays a video in 3D space
    /// </summary>
    /// <param name="videoFileName">The name of the video to display</param>
    /// <param name="location">The location in 3D space to place the video</param>
    /// <param name="scale">The factor by which to scale the video's size</param>
    /// <param name="path">The path to the folder in which the video can be found (defaults to timuli/videos/)</param>
    /// <returns></returns>
    GameObject DisplayVideo(string videoFileName, Vector3 location, float scale, string path = PARADIGM_PATH + "stimuli/videos/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject videoPlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "ImagePlanePrefab"));
        videoPlane.transform.position = location;
        videoPlane.name = videoFileName;

        //Load the vidoe onto the plane prefab
        videoPlayer = videoPlane.GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoOver;
        VideoClip clip = Resources.Load<VideoClip>(path + videoFileName);
        videoPlayer.clip = clip;

        Vector2 unitdim = new Vector2(videoPlayer.clip.width, videoPlayer.clip.height).normalized;
        videoPlane.transform.localScale = new Vector3(videoPlane.transform.localScale.x * unitdim.x * scale, videoPlane.transform.localScale.y * 1, videoPlane.transform.localScale.z * unitdim.y * scale);

        RecordData("Scaled size of " + videoFileName + ": (" + (unitdim.x * scale * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * scale * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return videoPlane;
    }

    /// <summary>
    /// Displays the ending thank-you message to the subject
    /// </summary>
    void DisplayEndMessage()
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        thankYou = Instantiate(Resources.Load(PARADIGM_PATH + "Thank_You") as GameObject);
        thankYou.transform.position = DISPLAY_LOCATION;
    }
    #endregion //display and video functions

    #region update functions
    /// <summary>
    /// This function is a callback for the stimuli videos displayed each round, and is called when they finish.
    /// It is not called when the instructions video finished.
    /// The code:
    ///     Removed the video from the scene
    ///     Dislpays the fixation dot if it is not already displayed
    ///     Sets the current event flag to state that the video ended
    /// </summary>
    /// <param name="vp">The video player that just stopped plpaying</param>
    void OnVideoOver(VideoPlayer vp)
    {
        videoPlayingDone = true;
        Destroy(videoPlane);
        videoCompletionTime = Time.time;

        //Write timing data now that video ended
        currentEvent = "video ended ";
        videoName = "";
        if (fixationDot == null)
        {
            fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
        }
    }

    /// <summary>
    /// Code to run once the fixation period on the fixation dotr is done.
    /// The method removed the fixation dot and displays the next round of stimuli.
    /// </summary>
    void OnFixationDone()
    {
        //Remove fixation dot 
        fixationDotCallibrationDone = true;
        Destroy(fixationDot);
        fixationDot = null;

        //Display video stimulus
        DisplayStimuli();
        currentEvent = "free looking";
        stimuliNames = q1ImageName + "," + q2ImageName + "," + q3ImageName + "," + q4ImageName + "," + STIMULUS_LIST[currentStimuliListIndex][4];
    }

    /// <summary>
    /// Code to run once the fixation period for the ready dot is over.
    /// It displays the stimulus video for the current round of stimuli
    /// </summary>
    void OnReadyDone()
    {
        //Remove ready dot 
        readyDotCallibrationDone = true;
        Destroy(readyDot);

        //play the video--this is the part where we are interested in user eye movement
        videoPlane = DisplayVideo(STIMULUS_LIST[currentStimuliListIndex][4], DISPLAY_LOCATION, IMAGE_SCALE);
        Destroy(fixationDot);
        fixationDot = null;

        //Write timing data now that video started
        currentEvent = "video started";
    }

    /// <summary>
    /// Runs through the experiment once training is done.
    /// Data is collected during this period and is flagged as experimental data.
    /// </summary>
    void RunExperiment()
    {
        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

        //Raycase: if there is a hit, process it
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //Callibrate using the fixation dot
            if (hit.transform.name.StartsWith(FIXATION_DOT))
            {
                //Still calibrating
                if (fixationDotEarliestContinuousHitTime < 0)
                {
                    fixationDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - fixationDotEarliestContinuousHitTime >= FIXATION_DOT_DISPLAY_TIME
                    && !fixationDotCallibrationDone)
                {
                    OnFixationDone();
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Calibration failed (user looked away)
                fixationDotEarliestContinuousHitTime = -1;
            }

            //Callibrate using the ready dot
            else if (hit.transform.name.StartsWith(READY_DOT))
            {
                //Still calibrating
                if (readyDotEarliestContinuousHitTime < 0)
                {
                    readyDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - readyDotEarliestContinuousHitTime >= READY_DOT_DISPLAY_TIME
                    && !readyDotCallibrationDone)
                {
                    OnReadyDone();
                }
            }
            else if (!readyDotCallibrationDone)
            {
                //Calibration failed (user looked away)
                readyDotEarliestContinuousHitTime = -1;
            }

            WriteHitData(hit);
        }

        //Control the display of the next stimulus once callibration is done
        if (fixationDotCallibrationDone
            && Time.time - imageDisplayStartTime >= IMAGE_INITIAL_DISPLAY_TIME_PRE_READY_DOT
            && !imageInitialDispayDone)
        {
            readyDot = DisplayFixationDot(DISPLAY_LOCATION, READY_DOT);

            //Now we need to flag the initial image display as done
            imageInitialDispayDone = true;
        }

        if (imageInitialDispayDone && readyDotCallibrationDone)
        {
            CheckReset();
        }
    }

    /// <summary>
    /// Checks whether there are more experiment rounds to run.
    ///     If so, loads the next round
    ///     If not, displays the end message
    /// </summary>
    void CheckReset()
    {
        //After the video has been done for a short time, end the experiment and rest for the next stimuli, if present
        if (videoPlayingDone && Time.time - videoCompletionTime >= POST_VIDEO_KEEP_DISPLAYING_IMAGES_TIME && !resetting)
        {
            //Increment the index is the stimuli list
            currentStimuliListIndex++;

            //Remove stimuli game objects
            foreach (GameObject go in CURRENT_STIMULI)
            {
                Destroy(go);
            }
            stimuliNames = ",,,,";

            //If there are not more prompts, prepare to terminate
            if (currentStimuliListIndex >= STIMULUS_LIST.Count)
            {
                //Set termination flags
                resetting = true;
                resettingStartTime = Time.time;

                //Write out all collected data to disk
                WriteData();

                //Display a thank-you message
                DisplayEndMessage();
            }
            //Else, reset for next round, with the same subject
            else
            {
                ResetForNextRound();
            }
        }
    }

    /// <summary>
    /// Displays instructions to the user. Only ends when the user fixates on the dot, which may be at any time they choose.
    /// </summary>
    /// <returns></returns>
    bool RunStartUp()
    {
        if (videoPlane == null)
        {
            videoPlane = DisplayVideo("VocabInstructions", DISPLAY_LOCATION, IMAGE_SCALE * 3);
            fixationDot = DisplayFixationDot(DISPLAY_LOCATION + new Vector3(0f, 3f, 0f), FIXATION_DOT);
        }

        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //Control fixation dot display time
            if (hit.transform.name.StartsWith(FIXATION_DOT))
            {
                //Still calibrating
                if (fixationDotEarliestContinuousHitTime < 0)
                {
                    fixationDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - fixationDotEarliestContinuousHitTime >= FIXATION_DOT_DISPLAY_TIME && !fixationDotCallibrationDone)
                {
                    fixationDotCallibrationDone = true;
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }

        if (fixationDotCallibrationDone)
        {
            //Reset vars
            fixationDotEarliestContinuousHitTime = -1;
            fixationDotCallibrationDone = false;
            Destroy(videoPlane);
            videoPlane = null;
            currentStimuliListIndex = 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Should be called while the training period is active.
    /// It determines when the trianing perioid needs to end and returns after that.
    ///     The user will see a message saying training is over and need to fixate on a fixation dot for this to occur.
    /// Data is collected during this period and is flagged as training data.
    /// </summary>
    /// <returns></returns>
    bool RunTraining()
    {
        if (text == null)
        {
            text = Instantiate(Resources.Load(PARADIGM_PATH + "studyText") as GameObject);
            text.transform.position = DISPLAY_LOCATION;
        }

        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //Control fixation dot display time
            if (hit.transform.name.StartsWith(FIXATION_DOT))
            {
                //Still calibrating
                if (fixationDotEarliestContinuousHitTime < 0)
                {
                    fixationDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - fixationDotEarliestContinuousHitTime >= FIXATION_DOT_DISPLAY_TIME && !fixationDotCallibrationDone)
                {
                    //Remove fixation dot 
                    fixationDotCallibrationDone = true;
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }

        if (fixationDotCallibrationDone)
        {
            //Cleanup
            currentEvent = "Beginning experiment (training ended)";
            Destroy(text);
            text = null;
            fixationDotCallibrationDone = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// This is the main program update loop. It is called every frame of program execution but the Unity backend.
    /// It manages transitioning between the major phases of the experiment:
    ///     Start-up (instructions)
    ///     Training
    ///     Experiment collection
    /// All of those phases are contained in separate funcitons; this code just dicides which one of them should be run.
    /// </summary>
    void Update()
    {
        if (currentStimuliListIndex == -1)
        {
            startUpOver = RunStartUp();
        }
        else if (currentStimuliListIndex == NUM_TRAINING_STIMULI && !trainingOver)
        {
            if (fixationDot == null)
            {
                fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
                fixationDotEarliestContinuousHitTime = -1;
                fixationDotCallibrationDone = false;
            }
            trainingOver = RunTraining();
        }

        if ((startUpOver && currentStimuliListIndex < NUM_TRAINING_STIMULI) || trainingOver)
        {
            RunExperiment();
        }
    }
    #endregion //update functions

    #region data functions

    /// <summary>
    /// Gets the current time as a string in a form that is safe to use for naming files (free of invalid special characters)
    /// </summary>
    /// <returns>a string representing the current time</returns>
    string getCleanDateTime()
    {
        return DateTime.Now.ToShortDateString().Replace('/', '-') + "__" + DateTime.Now.ToLongTimeString().Replace(':', '-');
    }

    /// <summary>
    /// Creates a string containing the exact x,y,z data of the given point, delimited by commas in the for x1,y1,z1
    /// </summary>
    /// <param name="point">the point whoe value is wanted</param>
    /// <returns>a string containing the loc. of the point to maximum precision</returns>
    string ExactPoint(Vector3 point)
    {
        return point.x + "," + point.y + "," + point.z;
    }

    /// <summary>
    /// Writes the recorded hit with proper formatting
    /// </summary>
    /// <param name="hit">The raycast his to record</param>
    void WriteHitData(RaycastHit hit)
    {
        string hitData;
        try
        {
            hitData = ExactPoint(hit.point);
        }
        catch (Exception ex)
        {
            hitData = ",None";
        }
        RecordData(Time.fixedTime.ToString() + "," + (Time.time - lastEventTriggerTime) + "," + hitData);
    }

    /// <summary>
    /// Writes the given data to the experimental data file. A few notes here:
    ///     data should be csv formatted
    ///     Data is not written immediately; instead, it is put into a buffer so that the program does not slow down with each call
    /// </summary>
    /// <param name="data">The data to write</param>
    /// <param name="isHeader">True if the data is indended to organize / label the ouput file rather then provide numeric output</param>
    void RecordData(string data, bool isHeader = false)
    {
        if (isHeader)
        {
            frameDataList.AddLast(data);
            lastEventTriggerTime = Time.time;
        }
        else
        {
            frameDataList.AddLast(stimuliNames + "," + (currentStimuliListIndex + 1) + "," + currentEvent + "," + data + ",");
        }

        if (frameDataList.Count >= FRAME_DATA_BUFFER_SIZE)
        {
            WriteData();
        }
    }

    /// <summary>
    /// Writes all data contained in the frameDataList to disk
    /// </summary>
    void WriteData()
    {
        while (frameDataList.Count > 0)
        {
            writer.WriteLine(frameDataList.First.Value);
            frameDataList.RemoveFirst();
        }
    }
    #endregion //data functions

    #region reset and quit functions
    /// <summary>
    /// Resets the experiment for the next round of stimuli
    /// </summary>
    void ResetForNextRound()
    {
        videoPlayer = null;
        fixationDotEarliestContinuousHitTime =
            readyDotEarliestContinuousHitTime = -1;
        imageDisplayStartTime
            = videoCompletionTime
            = resettingStartTime
            = 0;
        fixationDotCallibrationDone
            = readyDotCallibrationDone
            = imageInitialDispayDone
            = videoPlayingDone
            = resetting
            = false;

        currentEvent = "Moving to next stimulus round";
    }

    /// <summary>
    /// Called by Unity when the appliccation closing signal is triggered (such as when the user closes the app). It:
    ///     Writes all collected data to disk
    ///     Closes open resources to prevent memory leaks
    /// </summary>
    void OnApplicationQuit()
    {
        WriteData();
        writer.Close();
    }
    #endregion /reset and quit functions
}