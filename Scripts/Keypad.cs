using UdonSharp;
using UnityEngine;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;

// ReSharper disable MemberCanBeMadeStatic.Local
// ReSharper disable once CheckNamespace

// PORTING FROM FOORACK TO UWUTILS

namespace UwUtils
{
    [AddComponentMenu("UwUtils/Keypad")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Keypad : UdonSharpBehaviour
    {

        private readonly string AUTHOR = "Foorack";
        private readonly string VERSION = "3.6";
        [Space]
        [SerializeField] private string keypadPassword = "2580";
        [SerializeField] private bool hideDoorOnGranted = true;
        [SerializeField] private GameObject[] DoorObjects = new GameObject[0];
        [Space]
        [Header("User settings")]
        [SerializeField] private bool autoGrantAllowedUsers = false;
        [Tooltip("List of users that can just press the confirm button without the code to get permission")]
        public string[] allowList = new string[0];
        [Tooltip("List of users who even with the code cannot enter the code")]
        public string[] denyList = new string[0];
        [Space]
        [Header("Sound settings")]
        [SerializeField] private bool useAudioFeedback;
        [SerializeField] private AudioSource feedbackSource = null;
        [SerializeField] private AudioClip soundDenied = null;
        [SerializeField] private AudioClip soundGranted = null;
        [SerializeField] private AudioClip soundButton = null;
        [Space]
        [Header("Text display")]
        [Space]
        [SerializeField] private TextMeshProUGUI internalKeypadDisplay = null;
        [SerializeField] private string translationWaitcode = "PASSCODE"; // ReSharper disable once InconsistentNaming
        [SerializeField] private string translationDenied = "DENIED"; // ReSharper disable once InconsistentNaming
        [SerializeField] private string translationGranted = "GRANTED"; // ReSharper disable once InconsistentNaming
        [Space]
        [Header("Event trigger relays")
        [Space]
        [SerializeField] private UdonBehaviour[] programsClosed;
        [SerializeField] private UdonBehaviour[] programsDenied;
        [SerializeField] private UdonBehaviour[] programsGranted;
        [Space]
        [Header("Per passcode actions (Hover over)")]
        [Tooltip("When enabled, each code in 'additionalPasscodes' will toggle its own door in the 'additionalDoors' list, same for programsGranted")]
        [SerializeField] private bool additionalKeySeparation = false;
        [SerializeField] private string[] additionalPasscodes = new string[0];
        [SerializeField] private GameObject[] additionalDoors = new GameObject[0];
        [Space]
        [Header("Extra Functions/Settings")]
        [SerializeField] private bool teleportOnGrant = false;
        [SerializeField] private Transform teleportDestination;
        [Range(0, 1), Tooltip("Use only one character")]
        [SerializeField] private string replacePassWithChar = "*";
        [Tooltip("When clear is hit after getting access, will revert the state of the 'doors' back to its original state.")]
        [SerializeField] private bool RevertGrantedEffectsOnClear = true;
        [SerializeField] private GameObject[] ExtraObjectsToTurnOn = new GameObject[0];
        [SerializeField] private GameObject[] ExtraObjectsToTurnOff = new GameObject[0];
        [SerializeField] private bool alsoSendInteractEventOnGrantedPrograms = false;
        [SerializeField] private string eventNameOnClosed = "_keypadClosed";
        [SerializeField] private string eventNameOnDenied = "_keypadDenied";
        [SerializeField] private string eventNameOnGranted = "_keypadGranted";
        [Space]
        [Header("Advanced Section")]
        [SerializeField] private int MaxInputLength = 8;
        [SerializeField] private bool useDisplay = true;
        [Space]
        [Header("Warning: No support will be given if logging was disabled.")]
        [SerializeField] private bool enableLogging = true;

        // Debugging
        private string _keypadId;
        private string _prefix;

        // Keypad data storage
        private string _buffer;
        private string[] _solutions;
        private GameObject[] _doors;
        private string loadedString;

        #region Util Functions
        private void Log(string value)
        {
            if (enableLogging)
            {
                Debug.Log(_prefix + value, gameObject);
            }
        }
        private void LogWarning(string value)
        {
            if (enableLogging)
            {
                Debug.LogWarning(_prefix + value, gameObject);
            }
        }
        private void LogError(string value)
        {
            if (enableLogging)
            {
                Debug.LogError(_prefix + value, gameObject);
            }
        }
        private void Die()
        {
            // Crash.
            // ReSharper disable once PossibleNullReferenceException
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            ((string)null).ToString();
        }
        #endregion Util Functions

        public void Start()
        {
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            _keypadId = Random.value.ToString() + "(" + gameObject.name + ")";
            _prefix = "[Reava_/UwUtils/Keypad] [K-" + _keypadId + "] ";
            // Override disableDebugging here
            Debug.Log(_prefix + "Starting Keypad... Made by @" + AUTHOR + ". Version " + VERSION + ".");

            _buffer = "";

            if (keypadPassword == null)
            {
                LogError("Solution was null! Resetting to default value!");
                keypadPassword = "2580";
            }

            if (keypadPassword.Length < 1 || keypadPassword.Length > MaxInputLength)
            {
                LogError("Solution was empty or longer than "+ MaxInputLength +" in length! Generating random value for security.");
                keypadPassword = Random.value.ToString();
            }

            if (internalKeypadDisplay == null)
            {
                LogError("Display is not set! This is not supported! If you do not want a display then just disable the display object. Dying...");
                useDisplay = false;
            }

            if (allowList == null)
            {
                LogError("Allow list was null, setting to empty list...");
                allowList = new string[0];
            }
            if (denyList == null)
            {
                LogError("Deny list was null, setting to empty list...");
                denyList = new string[0];
            }

            if (allowList.Length > 999)
            {
                LogError("Allow list was larger than 999, this is most likely unintentional, resetting to 0.");
                allowList = new string[0];
            }
            if (denyList.Length > 999)
            {
                LogError("Allow list was larger than 999, this is most likely unintentional, resetting to 0.");
                denyList = new string[0];
            }

            if (additionalPasscodes == null)
            {
                LogError("Additional Solutions list was null, setting to empty list...");
                additionalPasscodes = new string[0];
            }
            if (DoorObjects == null)
            {
                LogError("Additional Doors list was null, setting to empty list...");
                DoorObjects = new GameObject[0];
            }

            if (additionalPasscodes.Length > 999)
            {
                LogError("Additional Solutions list was larger than 999, this is most likely unintentional, resetting to 0.");
                additionalPasscodes = new string[0];
            }
            if (DoorObjects.Length > 999)
            {
                LogError("Additional Doors list was larger than 999, this is most likely unintentional, resetting to 0.");
                DoorObjects = new GameObject[0];
            }

            if (additionalKeySeparation && additionalPasscodes.Length != DoorObjects.Length)
            {
                LogError("Key separation was enabled, but the number of additional solutions is not equal to the number of additional doors, " +
                    "resetting to False. Please read the documentation what this setting does or contact for help.");
                additionalKeySeparation = false;
            }
            Log("Additional key separation is: " + additionalKeySeparation);

            // Merge primary solution/door with additional solutions/doors.
            // This makes coding and loops more streamlined.
            _solutions = new string[additionalPasscodes.Length + 1];
            _doors = new GameObject[DoorObjects.Length + 1];
            _solutions[0] = keypadPassword;
            for (var i = 0; i != additionalPasscodes.Length; i++)
            {
                _solutions[i + 1] = additionalPasscodes[i];
            }
            for (var i = 0; i != DoorObjects.Length; i++)
            {
                _doors[i + 1] = DoorObjects[i];
            }

            internalKeypadDisplay.text = translationWaitcode;
            Log("Keypad started!");
        }

        // ReSharper disable once InconsistentNaming
        private void CLR()
        {
            Log("Passcode CLEAR!");
            internalKeypadDisplay.text = translationWaitcode;

            foreach (GameObject door in _doors)
            {
                if (!door) continue;
                door.SetActive(hideDoorOnGranted);
            }

            if (programsClosed != null)
            {
                foreach(UdonBehaviour prog in programsClosed)
                {
                    prog.SetProgramVariable("keypadCode", _buffer);
                    prog.SendCustomEvent(eventNameOnClosed);
                }
            }

            _buffer = "";
        }

        // ReSharper disable once InconsistentNaming
        private void OK()
        {
            var isOnAllow = false;
            var isOnDeny = false;
            var username = Networking.LocalPlayer == null ? "UnityEditor" : Networking.LocalPlayer.displayName;
            // Check if user is on allow list
            foreach (var entry in allowList)
            {
                if (entry == username)
                {
                    isOnAllow = true;
                }
            }
            // Check if user is on deny list
            foreach (var entry in denyList)
            {
                if (entry == username)
                {
                    isOnDeny = true;
                }
            }

            var isCorrect = false;
            GameObject correctDoor = null;
            for (var i = 0; i != _solutions.Length; i++)
            {
                if (_solutions[i] != _buffer) continue;
                isCorrect = true;
                if (i < _doors.Length)
                {
                    correctDoor = _doors[i];
                }
            }
            // Check if pass is correct and not on deny, or if is on allow list.
            if ((isCorrect && !isOnDeny) || isOnAllow)
            {
                Log(isOnAllow ? "GRANTED through allow list!" : "Passcode GRANTED!");
                internalKeypadDisplay.text = translationGranted;

                foreach (GameObject door in _doors)
                {
                    if (!door) continue;
                    if (additionalKeySeparation)
                    {
                        if (door == correctDoor)
                        {
                            door.SetActive(!hideDoorOnGranted);
                        }
                        else
                        {
                            door.SetActive(hideDoorOnGranted);
                        }
                    }
                    else
                    {
                        door.SetActive(!hideDoorOnGranted);
                    }
                }

                if (soundGranted != null)
                {
                    feedbackSource.PlayOneShot(soundGranted);
                }

                if (programsGranted != null)
                {
                    foreach(UdonBehaviour prog in programsGranted)
                    {
                        if (!prog) continue;
                        prog.SetProgramVariable("keypadCode", _buffer);
                        prog.SendCustomEvent(eventNameOnGranted);
                    }
                }

                _buffer = "";
            }
            else
            {
                // Do not announce to user that they are on deny list.
                Log("Passcode DENIED!");
                internalKeypadDisplay.text = translationDenied;

                foreach (var door in _doors)
                {
                    if (door == gameObject) continue;
                    door.SetActive(hideDoorOnGranted);
                }

                if (soundDenied != null) feedbackSource.PlayOneShot(soundDenied);

                if (programsDenied != null)
                {
                    foreach (UdonBehaviour prog in programsDenied)
                    {
                        if (!prog) continue;
                        prog.SetProgramVariable("keypadCode", _buffer);
                        prog.SendCustomEvent(eventNameOnDenied);
                    }
                }

                _buffer = "";
            }
        }

        private void PrintPassword()
        {
            var pass = "*";
            for (var i = 1; i < _buffer.Length; i++)
            {
                pass += " *";
            }

            internalKeypadDisplay.text = pass;
        }

        public void ButtonInput(string inputValue)
        {
            if (inputValue == "CLR")
            {
                CLR();
            }
            else if (inputValue == "OK")
            {
                OK();
            }
            else
            {
                if (_buffer.Length == 8)
                {
                    Log("Limit reached!");
                }
                else
                {
                    _buffer += inputValue;
                    PrintPassword();
                    Log("Buffer appended: " + inputValue);
                    if (soundButton != null)
                    {
                        feedbackSource.PlayOneShot(soundButton);
                    }
                }
            }
        }

    }
}