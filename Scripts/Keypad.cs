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
        [SerializeField] private string solution = "2580";
        [SerializeField] private GameObject[] DoorObjects = new GameObject[0];
        [Space]
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
        [SerializeField] private string translationPasscode = "PASSCODE"; // ReSharper disable once InconsistentNaming
        [SerializeField] private string translationDenied = "DENIED"; // ReSharper disable once InconsistentNaming
        [SerializeField] private string translationGranted = "GRANTED"; // ReSharper disable once InconsistentNaming
        [Space]
        [SerializeField] private bool hideDoorOnGranted = true;
        [Space]
        [SerializeField] private UdonBehaviour programClosed;
        [SerializeField] private UdonBehaviour programDenied;
        [SerializeField] private UdonBehaviour[] programGranted;
        [Space]
        [SerializeField] private TextMeshProUGUI internalKeypadDisplay = null;
        [Space]
        [SerializeField] private string[] additionalSolutions = new string[0];
        [Space]
        [SerializeField] private bool additionalKeySeparation = false;
        [Space]
        [Header("Fetch config from remote string? (See docs)")]
        [SerializeField] private bool useRemoteString = false;
        [SerializeField] private VRCUrl remoteConfigUrl;
        [HideInInspector] public string[] strArr;
        [Space]
        [Header("Warning: No support will be given if logging was disabled.")]
        public bool enableLogging = true;
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

            if (solution == null)
            {
                LogError("Solution was null! Resetting to default value!");
                solution = "2580";
            }

            if (solution.Length < 1 || solution.Length > 8)
            {
                LogError("Solution was shorter than 1 or longer than 8 in length! Generating random value for security.");
                solution = Random.value.ToString();
            }

            if (internalKeypadDisplay == null)
            {
                LogError("Display is not set! This is not supported! If you do not want a display then just disable the display object. Dying...");
                Die();
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

            if (additionalSolutions == null)
            {
                LogError("Additional Solutions list was null, setting to empty list...");
                additionalSolutions = new string[0];
            }
            if (DoorObjects == null)
            {
                LogError("Additional Doors list was null, setting to empty list...");
                DoorObjects = new GameObject[0];
            }

            if (additionalSolutions.Length > 999)
            {
                LogError("Additional Solutions list was larger than 999, this is most likely unintentional, resetting to 0.");
                additionalSolutions = new string[0];
            }
            if (DoorObjects.Length > 999)
            {
                LogError("Additional Doors list was larger than 999, this is most likely unintentional, resetting to 0.");
                DoorObjects = new GameObject[0];
            }

            if (additionalKeySeparation && additionalSolutions.Length != DoorObjects.Length)
            {
                LogError("Key separation was enabled, but the number of additional solutions is not equal to the number of additional doors, " +
                    "resetting to False. Please read the documentation what this setting does or contact for help.");
                additionalKeySeparation = false;
            }
            Log("Additional key separation is: " + additionalKeySeparation);

            // Merge primary solution/door with additional solutions/doors.
            // This makes coding and loops more streamlined.
            _solutions = new string[additionalSolutions.Length + 1];
            _doors = new GameObject[DoorObjects.Length + 1];
            _solutions[0] = solution;
            for (var i = 0; i != additionalSolutions.Length; i++)
            {
                _solutions[i + 1] = additionalSolutions[i];
            }
            for (var i = 0; i != DoorObjects.Length; i++)
            {
                _doors[i + 1] = DoorObjects[i];
            }

            internalKeypadDisplay.text = translationPasscode;
            _LoadUrl();
            Log("Keypad started!");
        }
        public void _LoadUrl()
        {
            VRCStringDownloader.LoadUrl(remoteConfigUrl, (IUdonEventReceiver)this);
        }
        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            loadedString += result.Result;
            if (useRemoteString)
            {
                strArr = loadedString.Split(',');
            }
            if (enableLogging) Debug.Log("[Reava_/UwUtils/Keypad]: String successfully loaded: " + loadedString + "On: " + gameObject.name, gameObject);
        }
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            Debug.LogError("[Reava_/UwUtils/Keypad]: String loading failed: " + result.Error + "| Error Code: " + result.ErrorCode + "On: " + gameObject.name, gameObject);
        }

        // ReSharper disable once InconsistentNaming
        private void CLR()
        {
            Log("Passcode CLEAR!");
            internalKeypadDisplay.text = translationPasscode;

            foreach (var door in _doors)
            {
                if (door != gameObject)
                {
                    door.SetActive(hideDoorOnGranted);
                }
            }

            if (programDenied != null)
            {
                programClosed.SetProgramVariable("keypadCode", _buffer);
                programClosed.SendCustomEvent("keypadClosed");
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

                foreach (var door in _doors)
                {
                    if (door == gameObject) continue;
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

                if (programGranted != null)
                {
                    foreach(UdonBehaviour prog in programGranted)
                    {
                        if (!prog) continue;
                        prog.SetProgramVariable("keypadCode", _buffer);
                        prog.SendCustomEvent("keypadGranted");
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

                if (programDenied != null)
                {
                    programDenied.SetProgramVariable("keypadCode", _buffer);
                    programDenied.SendCustomEvent("keypadDenied");
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