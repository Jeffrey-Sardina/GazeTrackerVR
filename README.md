# Gaze Tracker VR v0.1

## Background and intent
The presence of anticipatory / predictive eye moments in response to linguistic stimuli is the center of the visual world paradigm. At present, eye trackers are used for determining where a subject is looking at any given point in time. However, these devices can be quite expensive, limiting the number of questions that many research groups can effectively test. As a solution to this, we propose an alternate method based on the intrinsic gaze-tracking ability of virtual reality (VR) systems. This gaze-tracking procedure has three major benefits: it minimizes the cost of such experiments, streamlines the implementation of such experiments, and increases the portability of gaze-tracking technology.

The idea behind Gaze Tracker is very simple. Since VR uses the gyroscopic motion-sensing ability of a headset to map a subject’s head motion to movement of the camera focal point in a 3D scene, a simple VR program that displays a set of stimuli can record, frame-by-frame, the direction of the subject’s main visual field. This yield very high-resolution data; a 50 frame-per-second (fps) system could log the location of the subject visual field at 3000 points over the course of a minute. This is precisely what Gaze Tracker does: it displays a stimulus, allows the subject to examine it, and records where the subject looks every frame.

In this specific case, we examined the use of a Gaze Tracker system for a variant of the visual world paradigm. A large image was displayed, centered in the subject’s visual field but extending well beyond it so that the subject would have to look around to examine what the image contained. All images used were transitive event images such as “CLOWN PUSH TV”. Subjects were given the VR headset and told to examine the image and sign in ASL what they saw. Initial results revealed that, not only was the Gaze Tracker system able to successfully determine where the subject were looking, but that patterns of looking were consistent between subjects as they explored the event.

All of this data was collected using only a VR headset and a smartphone capable to running the Gaze Tracker app. The simplicity of the system allows it to travel with researchers almost anywhere, and the lack of prohibitive costs make it feasible for many more labs to use than a conventional eye-tracking system. Moreover, the Gaze Tracker app is self-contained: all calibration,data collection, and stimulus display occur within the VR application itself, not requiring direct action from the researchers besides initial set-up.

The goal of the Gaze Tracker system is to be cheap and easy to implement. By releasing it as an open-sourced, BSD-licensed project, we hope to allow other research groups to apply the new gaze-tracking paradigm for the same purpose as traditional eye tracking methods. Moreover, since VR-based gaze tracking is a novel linguistic paradigm, we envision it as driving a new set of questions and experiments for linguistic analysis.

## Overview
Gaze Tracker is a VR application build to perform linguistic experiments that involve tracking a user's gaze in response to stimuli. It runs on Android (iOS is not supported). The purpose of Gaze Tracker is to make experiments faster, cheaper, and easier to manage. Overall, it allows:

- Tracking VR headset motion to determine the direction of the user's gaze
- Recording the location of the user's center of vision
- Running different experiments from one application

Different experiments will do this for different purposes. It is possible to add your own experiments by editing or adding code; see the Extending Gaze Tracker section for suggestions on how to add in your own experimental paradigms.

Gaze Tracker is currently in development. Directions for future development are:
- Easier editing of experiments
- More built-in experimental paradigms
- Easier editing of existing experimental paradigms
- Ability to use a handheld controller with the VR headset to provide input


### Technical
Development occurs on Unity 2018.3.11f1. I have tried to upgrade to Unity 2019, although all versions of Unity 2019 that I have tested have not been fully compatible with the existing VR code. I suggest you do not use Unity 2019 to develop and use Gaze Tracker for now, but rather use a 2018 build.

VR integration is done via the [Google VR SDK for Unity](https://developers.google.com/vr/develop/unity/get-started-android). 

Code is written in C#.


## General Setup


### Installing
Download Unity 2018 (not 2019) from [Unity's website](https://store.unity.com/download)

Follow the directions for setting up the [Google VR SDK for Unity](https://developers.google.com/vr/develop/unity/get-started-android). TODO

Copy the repo code into the new project. To do this:
- **Do not** overwrite the existing Assets folder in your new project, this will remove the Google VR tools form the project.
- Copy the Resources folder (Assets/Resources in the [Gaze Tracker repo](https://github.com/Jeffrey-Sardina/GazeTrackerVR)) into the new project's Assets folder.
- Copy the Scenes folder (Assets/Resources in the [Gaze Tracker repo](https://github.com/Jeffrey-Sardina/GazeTrackerVR)) into the new project's Scenes folder.


### Adding your own stimuli
Every paradigm has its own Experiment_Data folder, with a data text files inside. To add your own stimuli, put the names of those stimuli into the data file, and then copy your stimuli images / videos / audio into the 'stimuli' folder.

Different paradigms currently have different types of stimuli they accept. The next section (TODO) will cover what these paradigms are, as well as that the parameters in the data file mean for them.


### Building the App into your Android
Building the app can be done directly from unity. To do this, you need:
- The computer running Unity
- An compatible Android phone with developer mode enabled

To build the app:
- Plug your phone into the computer via USB
- From Unity, select File => Build and Run
- Unity will the automatically build the app onto your phone. You can then use it like any other app.