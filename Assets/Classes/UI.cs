using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using Auth;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI; 
using System.Text;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Linq;  
using System.Globalization;
using DB;
using Game;
using System.Threading.Tasks;

namespace  UI {
    public static class ResearchUI {

        private static GameObject MainContentGameObject;
        private static GameObject ResearchBackgroundGameObject;
        private static GameObject MainNavigationGameObject;
        private static Transform ResearchButtonsGameObjectTransform;
        private static Transform MainNavigationGameObjectTransform;

        private static GameObject ResearchButtonGameObject;
        private static string ResearchButtonDefaultSprite;
        private static string ResearchButtonCurrentSprite;
        private static Button ResearchButton;
        private static Image ResearchImg;

        private static GameObject BaseButtonGameObject;
        private static string BaseButtonDefaultSprite;
        private static string BaseButtonCurrentSprite;
        private static Button BaseButton;
        private static Image BaseImg;

        private static GameObject MilitaryButtonGameObject;
        private static string MilitaryButtonDefaultSprite;
        private static string MilitaryButtonCurrentSprite;
        private static Button MilitaryButton;
        private static Image MilitaryImg;

        private static GameObject CommanderButtonGameObject;
        private static string CommanderButtonDefaultSprite;
        private static string CommanderButtonCurrentSprite;
        private static Button CommanderButton;
        private static Image CommanderImg;

        private static GameObject CloseButtonGameObject;
        private static Button CloseButton;
        private static bool IsCloseButtonHooked;
        private static GameObject LevelUpButtonGameObject;
        private static Button LevelUpButton;
        private static GameObject StartResearchButtonGameObject;
        private static Button StartResearchButton;
        private static ListenerRegistration ProfileListenerRegistration;
        private static string ProfileListenerUserId;

        private static readonly Dictionary<Research.ResearchTypeEnum, string> NavigationSpritePaths =
            new Dictionary<Research.ResearchTypeEnum, string>
            {
                { Research.ResearchTypeEnum.Research, "UI/BuffIcons/ResearchIcon" },
                { Research.ResearchTypeEnum.Base, "UI/BuffIcons/BaseBuff" },
                { Research.ResearchTypeEnum.Military, "UI/BuffIcons/MilitaryFormation" },
                { Research.ResearchTypeEnum.Commander, "UI/BuffIcons/Commander" }
            };
        private static readonly Dictionary<GameObject, Buff> ButtonBuffLookup = new Dictionary<GameObject, Buff>();
        private static GameObject SelectedButtonGameObject;
        private static Buff SelectedBuff;
        private static readonly Color SelectedButtonTint = new Color(0.7f, 0.85f, 1f, 1f);

        private static GameObject CurrentButtonClicked;
        public static string ResearchType;



        private static Profile Profile;


        public static bool IsActive = true;

        private static Research Research;
        
        public static bool ShowResearchUI = false;
        public static bool DidShowResearchUI = false;
        public static GameObject ResearchRankTextGameObject;
        public static TMP_Text ResearchRankText;
        public static TMP_Text ResearchLabelText;
        public static TMP_Text ResearchDescriptionText;
        public static event Action ResearchLevelsChanged;
        

        private static int ResearchLevel;

        public static async void InitUI () {

            MainContentGameObject = GameObject.Find("MainContent");
            if (MainContentGameObject == null)
            {
                Debug.LogWarning("ResearchUI.InitUI aborted – 'MainContent' root not found in the active scene.");
                return;
            }

            Transform ResearchBackgroundGameObjectTransform = MainContentGameObject.transform.Find("ResearchBackground");
            if (ResearchBackgroundGameObjectTransform == null)
            {
                Debug.LogWarning("ResearchUI.InitUI aborted – 'ResearchBackground' was not found under 'MainContent'.");
                return;
            }
            ResearchBackgroundGameObject = ResearchBackgroundGameObjectTransform.gameObject;

            MainNavigationGameObjectTransform = MainContentGameObject.transform.Find("MainNavigation");
            if (MainNavigationGameObjectTransform == null)
            {
                Debug.LogWarning("ResearchUI.InitUI aborted – 'MainNavigation' was not found under 'MainContent'.");
                return;
            }
            MainNavigationGameObject = MainNavigationGameObjectTransform.gameObject;

            ResearchButtonsGameObjectTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchButtonGroup/ResearchButtons");
            if (ResearchButtonsGameObjectTransform == null)
            {
                Debug.LogWarning("ResearchUI.InitUI aborted – 'ResearchButtons' container was not found.");
                return;
            }

            CacheResearchUiElements();

            WireUpCloseButton();

            await EnsureProfileLoaded();
            SubscribeToProfileUpdates();
            await SetResearch(Research.ResearchTypeEnum.Research);

            MakeButtons(MainNavigationGameObjectTransform, true);

            UpdateResearchLabel();



        }

        public static void Render () {
            
        }

        public static void MakeButtons(Transform ParentTransform, bool isDefault) {
            if (ParentTransform == null)
            {
                Debug.LogWarning("Cannot create buttons without a parent transform.");
                return;
            }

            if (isDefault)
            {
                ResearchButtonDefaultSprite = GetNavigationSpritePath(Research.ResearchTypeEnum.Research);
                BaseButtonDefaultSprite = GetNavigationSpritePath(Research.ResearchTypeEnum.Base);
                MilitaryButtonDefaultSprite = GetNavigationSpritePath(Research.ResearchTypeEnum.Military);
                CommanderButtonDefaultSprite = GetNavigationSpritePath(Research.ResearchTypeEnum.Commander);

                ConfigureNavigationButton(
                    ParentTransform,
                    "Research-Default",
                    Research.ResearchTypeEnum.Research,
                    ResearchButtonDefaultSprite,
                    ref ResearchButtonGameObject,
                    ref ResearchButton,
                    ref ResearchImg);

                ConfigureNavigationButton(
                    ParentTransform,
                    "Base-Default",
                    Research.ResearchTypeEnum.Base,
                    BaseButtonDefaultSprite,
                    ref BaseButtonGameObject,
                    ref BaseButton,
                    ref BaseImg);

                ConfigureNavigationButton(
                    ParentTransform,
                    "Military-Default",
                    Research.ResearchTypeEnum.Military,
                    MilitaryButtonDefaultSprite,
                    ref MilitaryButtonGameObject,
                    ref MilitaryButton,
                    ref MilitaryImg);

                ConfigureNavigationButton(
                    ParentTransform,
                    "Commander-Default",
                    Research.ResearchTypeEnum.Commander,
                    CommanderButtonDefaultSprite,
                    ref CommanderButtonGameObject,
                    ref CommanderButton,
                    ref CommanderImg);

                return;
            }

            if (Research?.BuffList == null || Research.BuffList.Length == 0)
            {
                Debug.LogWarning("Research data is not available. Skipping button creation.");
                return;
            }

            ClearSelectedBuff();
            DeleteAllChildren(ParentTransform);

            Buff researchBuff = Research.BuffList.Length > 0 ? Research.BuffList[0] : null;
            Buff baseBuff = Research.BuffList.Length > 1 ? Research.BuffList[1] : null;
            Buff militaryBuff = Research.BuffList.Length > 2 ? Research.BuffList[2] : null;
            Buff commanderBuff = Research.BuffList.Length > 3 ? Research.BuffList[3] : null;

            ResearchButtonCurrentSprite = researchBuff?.SpritePath;
            BaseButtonCurrentSprite = baseBuff?.SpritePath;
            MilitaryButtonCurrentSprite = militaryBuff?.SpritePath;
            CommanderButtonCurrentSprite = commanderBuff?.SpritePath;

            if (researchBuff != null)
            {
                CreateButton(
                    ParentTransform,
                    ref ResearchButtonGameObject,
                    ref ResearchButton,
                    ref ResearchImg,
                    researchBuff.Id,
                    Research.ResearchTypeEnum.Research.ToString(),
                    ResearchButtonCurrentSprite,
                    researchBuff);
            }

            if (baseBuff != null)
            {
                CreateButton(
                    ParentTransform,
                    ref BaseButtonGameObject,
                    ref BaseButton,
                    ref BaseImg,
                    baseBuff.Id,
                    Research.ResearchTypeEnum.Base.ToString(),
                    BaseButtonCurrentSprite,
                    baseBuff);
            }

            if (militaryBuff != null)
            {
                CreateButton(
                    ParentTransform,
                    ref MilitaryButtonGameObject,
                    ref MilitaryButton,
                    ref MilitaryImg,
                    militaryBuff.Id,
                    Research.ResearchTypeEnum.Military.ToString(),
                    MilitaryButtonCurrentSprite,
                    militaryBuff);
            }

            if (commanderBuff != null)
            {
                CreateButton(
                    ParentTransform,
                    ref CommanderButtonGameObject,
                    ref CommanderButton,
                    ref CommanderImg,
                    commanderBuff.Id,
                    Research.ResearchTypeEnum.Commander.ToString(),
                    CommanderButtonCurrentSprite,
                    commanderBuff);
            }
        }

        private static void ShowResearchUIUpdate() {

        }

        private static void GetButtonsOnScreen(string GameObjectName = "Default") {
            if (Research.BuffList.Any(research => research.Id == GameObjectName)) {


                ResearchButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[0].Id).gameObject;
                ResearchImg = ResearchButtonGameObject.AddComponent<Image>();
                ResearchImg.sprite = Resources.Load<Sprite>(ResearchButtonCurrentSprite); 

                BaseButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[1].Id).gameObject;
                BaseImg = BaseButtonGameObject.AddComponent<Image>();
                BaseImg.sprite = Resources.Load<Sprite>(BaseButtonCurrentSprite);

                MilitaryButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[2].Id).gameObject;
                MilitaryImg = MilitaryButtonGameObject.AddComponent<Image>();
                MilitaryImg.sprite = Resources.Load<Sprite>(MilitaryButtonCurrentSprite);

                CommanderButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[3].Id).gameObject;
                CommanderImg = CommanderButtonGameObject.AddComponent<Image>();
                CommanderImg.sprite = Resources.Load<Sprite>(CommanderButtonCurrentSprite);
            }
            if (GameObjectName == "Default") { // <-------- includes default

                ResearchButtonGameObject = MainNavigationGameObject.transform.Find("Research-Default").gameObject;
                ResearchImg = ResearchButtonGameObject.AddComponent<Image>();
                ResearchImg.sprite = Resources.Load<Sprite>(ResearchButtonDefaultSprite); 

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Base-Default").gameObject;
                BaseImg = BaseButtonGameObject.AddComponent<Image>();
                BaseImg.sprite = Resources.Load<Sprite>(BaseButtonDefaultSprite);

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Military-Default").gameObject;
                MilitaryImg = MilitaryButtonGameObject.AddComponent<Image>();
                MilitaryImg.sprite = Resources.Load<Sprite>(MilitaryButtonDefaultSprite);

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Commander-Default").gameObject;
                CommanderImg = CommanderButtonGameObject.AddComponent<Image>();
                CommanderImg.sprite = Resources.Load<Sprite>(CommanderButtonDefaultSprite);
            }
        }
        
        private static async void ResearchButtonClicked(GameObject button)
        {
            if (button == null)
            {
                return;
            }

            if (ButtonBuffLookup.TryGetValue(button, out var associatedBuff) && associatedBuff != null)
            {
                HandleBuffSelection(button, associatedBuff);
                return;
            }

            if (!Enum.TryParse(button.tag, true, out Research.ResearchTypeEnum researchType))
            {
                Debug.LogWarning($"Unhandled research button tag: {button.tag}");
                return;
            }

            bool isDefaultButton = string.Equals(button.name, "Default", StringComparison.OrdinalIgnoreCase)
                                    || button.name.EndsWith("-Default", StringComparison.OrdinalIgnoreCase);

            if (isDefaultButton)
            {
                await SetResearch(researchType);
                CurrentButtonClicked = button;
                if (ResearchButtonsGameObjectTransform != null)
                {
                    DeleteAllChildren(ResearchButtonsGameObjectTransform);
                }
                ShowResearchBackground(true);
                MakeButtons(ResearchButtonsGameObjectTransform , false);
                return;
            }

            ShowResearchBackground(false);
        }

        private static void ShowResearchBackground(bool show) {
            if (show == true) {
                ResearchBackgroundGameObject.SetActive(true);
                MainNavigationGameObject.SetActive(false);
                UpdateResearchLabel();
                UpdateLevelUpButtonVisibility();
                UpdateStartResearchButtonState();
            } else {
                ResearchBackgroundGameObject.SetActive(false);
                MainNavigationGameObject.SetActive(true);
                ClearSelectedBuff();
            }
        }

        private static void HandleBuffSelection(GameObject button, Buff buff)
        {
            if (buff == null)
            {
                return;
            }

            EnsureCurrentResearchInitialized();
            if (SelectedButtonGameObject != null && SelectedButtonGameObject.TryGetComponent(out Image previousImage))
            {
                previousImage.color = Color.white;
            }

            SelectedButtonGameObject = button;
            SelectedBuff = buff;

            if (SelectedButtonGameObject != null && SelectedButtonGameObject.TryGetComponent(out Image newImage))
            {
                newImage.color = SelectedButtonTint;
            }

            UpdateResearchDescription(buff.BuffDescription);
            UpdateStartResearchButtonState();
        }

        private static async Task EnsureProfileLoaded()
        {
            if (Profile != null)
            {
                return;
            }

            if (Auth.User.user == null)
            {
                Debug.LogWarning("Cannot load profile without an authenticated user.");
                return;
            }

            if (DB.Default.GameProfiles == null)
            {
                DB.Default.Init();
            }

            if (DB.Default.GameProfiles == null)
            {
                Debug.LogWarning("GameProfiles collection is not initialised; cannot load profile.");
                return;
            }

            DocumentReference gameProfileRef = DB.Default.GameProfiles.Document(Auth.User.user.UserId);
            DocumentSnapshot gameProfileDoc = await gameProfileRef.GetSnapshotAsync();
            Profile = gameProfileDoc.ConvertTo<Profile>() ?? new Profile();
            EnsureCurrentResearchInitialized();
        }

        private static void EnsureCurrentResearchInitialized()
        {
            if (Profile == null)
            {
                Profile = new Profile();
            }
            if (Profile.ResearchLevels == null)
            {
                Profile.ResearchLevels = new ResearchLevels();
            }
            if (Profile.CurrentResearch == null)
            {
                Profile.CurrentResearch = new CurrentResearch();
            }
            if (Profile.CurrentResearch.CurrentResearches == null)
            {
                Profile.CurrentResearch.CurrentResearches = new List<Buff>();
            }
            if (Profile.CurrentResearch.CurrentResearchStarted == null)
            {
                Profile.CurrentResearch.CurrentResearchStarted = string.Empty;
            }
            if (Profile.CurrentResearch.CurrentResearchFinished == null)
            {
                Profile.CurrentResearch.CurrentResearchFinished = string.Empty;
            }
            if (Profile.CurrentResearch.Research == null)
            {
                Profile.CurrentResearch.Research = new Research();
            }
        }

        private static void SynchronizeCurrentResearchBuffs()
        {
            if (Profile?.CurrentResearch?.CurrentResearches == null)
            {
                return;
            }

            if (Research?.BuffList == null || Research.BuffList.Length == 0)
            {
                Profile.CurrentResearch.CurrentResearches.Clear();
                return;
            }

            var validIds = new HashSet<string>(Research.BuffList.Where(b => b != null && !string.IsNullOrEmpty(b.Id)).Select(b => b.Id));
            Profile.CurrentResearch.CurrentResearches = Profile.CurrentResearch.CurrentResearches
                .Where(b => b != null && !string.IsNullOrEmpty(b.Id) && validIds.Contains(b.Id))
                .ToList();
        }

        private static int GetResearchLevelForType(Research.ResearchTypeEnum researchType)
        {
            if (Profile?.ResearchLevels == null)
            {
                return 0;
            }

            switch (researchType)
            {
                case Research.ResearchTypeEnum.Research:
                    return Mathf.Max(0, Profile.ResearchLevels.Research);
                case Research.ResearchTypeEnum.Base:
                    return Mathf.Max(0, Profile.ResearchLevels.Base);
                case Research.ResearchTypeEnum.Military:
                    return Mathf.Max(0, Profile.ResearchLevels.Military);
                case Research.ResearchTypeEnum.Commander:
                    return Mathf.Max(0, Profile.ResearchLevels.Comander);
                default:
                    return 0;
            }
        }

        private static async Task<Research> LoadResearchAsync(Research.ResearchTypeEnum researchType, int level)
        {
            CollectionReference gameConfig = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches");
            Query query = gameConfig
                .WhereEqualTo("ResearchLevel", level)
                .WhereEqualTo("ResearchType", (int)researchType)
                .Limit(1);
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            DocumentSnapshot researchDoc = snapshot.Documents.FirstOrDefault();
            return researchDoc != null ? researchDoc.ConvertTo<Research>() : null;
        }

        private static async Task SetResearch(Research.ResearchTypeEnum researchType)
        {
            await EnsureProfileLoaded();
            EnsureCurrentResearchInitialized();
            ResearchType = researchType.ToString();
            await CleanupCompletedResearchesAsync();
            int levelFromProfile = GetResearchLevelForType(researchType);
            ResearchLevel = levelFromProfile;
            Research = await LoadResearchAsync(researchType, levelFromProfile);
            if (Research == null)
            {
                Debug.LogWarning($"No research config found for type {researchType} at level {levelFromProfile}. Falling back to level 0.");
                if (levelFromProfile != 0)
                {
                    Research = await LoadResearchAsync(researchType, 0);
                }
            }
            if (Research == null)
            {
                Debug.LogWarning($"No research config found for type {researchType} at level 0. Research UI will display profile level without config details.");
                Profile.CurrentResearch.CurrentResearches.Clear();
                Profile.CurrentResearch.Research = new Research();
            }
            else
            {
                ResearchLevel = Mathf.Max(levelFromProfile, Research.ResearchLevel);
                SynchronizeCurrentResearchBuffs();
                Profile.CurrentResearch.Research = Research;
            }
            UpdateResearchRankLabel();
            UpdateResearchLabel();
            UpdateResearchDescription(Research?.ResearchDescription ?? string.Empty);
            UpdateLevelUpButtonVisibility();
            UpdateStartResearchButtonState();
            RefreshCurrentResearchButtons();
            if (Research != null)
            {
                ResearchLevelsChanged?.Invoke();
            }
        }

        private static void SubscribeToProfileUpdates()
        {
            if (Auth.User.user == null)
            {
                Debug.LogWarning("Cannot subscribe to profile updates without an authenticated user.");
                return;
            }

            string currentUserId = Auth.User.user.UserId;

            if (ProfileListenerRegistration != null)
            {
                if (string.Equals(ProfileListenerUserId, currentUserId, StringComparison.Ordinal))
                {
                    return;
                }

                ProfileListenerRegistration.Stop();
                ProfileListenerRegistration = null;
                ProfileListenerUserId = null;
                Profile = null;
            }

            if (DB.Default.GameProfiles == null)
            {
                DB.Default.Init();
            }

            if (DB.Default.GameProfiles == null)
            {
                Debug.LogWarning("GameProfiles collection is not initialised; cannot subscribe to profile updates.");
                return;
            }

            DocumentReference profileRef = DB.Default.GameProfiles.Document(currentUserId);
            ProfileListenerRegistration = profileRef.Listen(snapshot =>
            {
                if (snapshot == null)
                {
                    return;
                }

                if (!snapshot.Exists)
                {
                    Profile = new Profile();
                    EnsureCurrentResearchInitialized();
                    LogProfileSnapshot(Profile, "Profile listener (document missing)");
                    ResearchLevel = 0;
                    UpdateResearchRankLabel();
                    UpdateResearchLabel();
                    return;
                }

                Profile = snapshot.ConvertTo<Profile>() ?? new Profile();
                EnsureCurrentResearchInitialized();
                LogProfileSnapshot(Profile, "Profile listener update");
                Research.ResearchTypeEnum currentType = GetCurrentResearchType();
                _ = SetResearch(currentType);
            });
            ProfileListenerUserId = currentUserId;
        }

        private static Research.ResearchTypeEnum GetCurrentResearchType()
        {
            if (!string.IsNullOrEmpty(ResearchType) &&
                Enum.TryParse(ResearchType, out Research.ResearchTypeEnum parsedType))
            {
                return parsedType;
            }

            return Research.ResearchTypeEnum.Research;
        }

        private static void CreateButton(
            Transform parent,
            ref GameObject buttonGameObject,
            ref Button buttonComponent,
            ref Image imageComponent,
            string name,
            string tag,
            string spritePath,
            Buff associatedBuff = null)
        {
            buttonGameObject = new GameObject(name);
            RectTransform rectTransform = buttonGameObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160f, 160f);
            buttonGameObject.tag = tag;
            buttonComponent = buttonGameObject.AddComponent<Button>();
            imageComponent = buttonGameObject.AddComponent<Image>();
            imageComponent.sprite = LoadSprite(spritePath);
            buttonGameObject.transform.SetParent(parent, false);
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            GameObject capturedButton = buttonGameObject;
            buttonComponent.onClick.AddListener(delegate { ResearchButtonClicked(capturedButton); });
            ButtonBuffLookup.Remove(buttonGameObject);
            if (associatedBuff != null)
            {
                ButtonBuffLookup[buttonGameObject] = associatedBuff;
                Buff activeBuffData = FindActiveCurrentResearchBuff(associatedBuff.Id);
                if (activeBuffData != null && !IsBuffComplete(activeBuffData))
                {
                    buttonComponent.interactable = false;
                    AttachCountdownOverlay(buttonGameObject, activeBuffData);
                }
                else
                {
                    buttonComponent.interactable = true;
                    AttachCountdownOverlay(buttonGameObject, null);
                }
            }
            else
            {
                AttachCountdownOverlay(buttonGameObject, null);
            }
        }

        private static void ConfigureNavigationButton(
            Transform parent,
            string childName,
            Research.ResearchTypeEnum researchType,
            string spritePath,
            ref GameObject buttonGameObject,
            ref Button buttonComponent,
            ref Image imageComponent)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                CreateButton(
                    parent,
                    ref buttonGameObject,
                    ref buttonComponent,
                    ref imageComponent,
                    childName,
                    researchType.ToString(),
                    spritePath);
                return;
            }

            buttonGameObject = child.gameObject;
            buttonGameObject.name = childName;
            buttonGameObject.tag = researchType.ToString();
            ButtonBuffLookup.Remove(buttonGameObject);

            buttonComponent = buttonGameObject.GetComponent<Button>() ?? buttonGameObject.AddComponent<Button>();
            buttonComponent.onClick.RemoveAllListeners();

            imageComponent = buttonGameObject.GetComponent<Image>() ?? buttonGameObject.AddComponent<Image>();
            imageComponent.sprite = LoadSprite(spritePath);

            GameObject capturedButton = buttonGameObject;
            buttonComponent.onClick.AddListener(delegate { ResearchButtonClicked(capturedButton); });
            AttachCountdownOverlay(buttonGameObject, null);
        }

        private static void CacheResearchUiElements()
        {
            if (MainContentGameObject == null)
            {
                return;
            }

            if (ResearchRankText == null || ResearchRankTextGameObject == null)
            {
                Transform rankTextTransform = MainContentGameObject.transform.Find("ResearchBackground/ReaserchRankGroup/ResearchRankText");
                if (rankTextTransform != null)
                {
                    ResearchRankTextGameObject = rankTextTransform.gameObject;
                    ResearchRankText = ResearchRankTextGameObject.GetComponent<TMP_Text>();
                }
                else
                {
                    Debug.LogWarning("ResearchRankText GameObject not found in scene hierarchy.");
                }
            }

            if (ResearchLabelText == null)
            {
                Transform labelTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchButtonGroup/ResearchLabel");
                if (labelTransform == null)
                {
                    labelTransform = MainContentGameObject.transform.Find("ResearchBackground/ReaserchRankGroup/ResearchLabel");
                }
                if (labelTransform == null)
                {
                    labelTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchLabel");
                }
                if (labelTransform != null)
                {
                    ResearchLabelText = labelTransform.GetComponent<TMP_Text>();
                }
                else
                {
                    Debug.LogWarning("ResearchLabel GameObject not found in scene hierarchy.");
                }
            }

            if (LevelUpButtonGameObject == null)
            {
                Transform levelUpTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchButtonGroup/LevelUpButton");
                if (levelUpTransform != null)
                {
                    LevelUpButtonGameObject = levelUpTransform.gameObject;
                    LevelUpButton = LevelUpButtonGameObject.GetComponent<Button>() ?? LevelUpButtonGameObject.AddComponent<Button>();
                }
                else
                {
                    Debug.LogWarning("LevelUpButton GameObject not found in scene hierarchy.");
                }
            }

            if (StartResearchButtonGameObject == null)
            {
                Transform startTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchDetails/StudyResearchButton");
                if (startTransform != null)
                {
                    StartResearchButtonGameObject = startTransform.gameObject;
                    StartResearchButton = StartResearchButtonGameObject.GetComponent<Button>() ?? StartResearchButtonGameObject.AddComponent<Button>();
                    WireUpStartResearchButton();
                }
                else
                {
                    Debug.LogWarning("StudyResearchButton GameObject not found in scene hierarchy.");
                }
            }
            else if (StartResearchButton != null)
            {
                WireUpStartResearchButton();
            }

            if (ResearchDescriptionText == null)
            {
                Transform descriptionTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchDetails/ResearchDescription");
                if (descriptionTransform != null)
                {
                    ResearchDescriptionText = descriptionTransform.GetComponent<TMP_Text>();
                }
                else
                {
                    Debug.LogWarning("ResearchDescription GameObject not found in scene hierarchy.");
                }
            }

            UpdateLevelUpButtonVisibility();
            UpdateStartResearchButtonState();
        }

        private static void UpdateResearchRankLabel()
        {
            CacheResearchUiElements();
            if (ResearchRankText == null)
            {
                return;
            }

            Research.ResearchTypeEnum currentType = GetCurrentResearchType();
            RunOnMainThread(() =>
            {
                if (ResearchRankText != null)
                {
                    ResearchRankText.text = $"Rank {Mathf.Max(0, ResearchLevel)}";
                }
            });
        }

        private static void UpdateResearchLabel()
        {
            CacheResearchUiElements();
            if (ResearchLabelText == null)
            {
                return;
            }

            string typeLabel = GetCurrentResearchType().ToString();
            RunOnMainThread(() =>
            {
                if (ResearchLabelText != null)
                {
                    ResearchLabelText.text = typeLabel;
                }
            });
        }

        private static void ClearSelectedBuff()
        {
            if (SelectedButtonGameObject != null && SelectedButtonGameObject.TryGetComponent(out Image image))
            {
                image.color = Color.white;
            }
            SelectedButtonGameObject = null;
            SelectedBuff = null;
            UpdateStartResearchButtonState();
            UpdateResearchDescription(Research?.ResearchDescription ?? string.Empty);
        }

        private static void UpdateLevelUpButtonVisibility()
        {
            EnsureCurrentResearchInitialized();
            int activeCount = Profile.CurrentResearch.CurrentResearches
                .Count(buff => !IsBuffComplete(buff));
            bool hasRequired = activeCount >= 2;
            if (LevelUpButtonGameObject != null)
            {
                LevelUpButtonGameObject.SetActive(hasRequired);
            }
        }

        private static void UpdateStartResearchButtonState()
        {
            if (StartResearchButton == null)
            {
                return;
            }

            EnsureCurrentResearchInitialized();
            int maxSimultaneous = Mathf.Max(1, Profile.CurrentResearch.ResearchesAtATime);
            int activeCount = Profile.CurrentResearch.CurrentResearches
                .Count(buff => !IsBuffComplete(buff));
            bool canStartMore = activeCount < maxSimultaneous;
            bool hasSelection = SelectedBuff != null &&
                                FindActiveCurrentResearchBuff(SelectedBuff.Id) == null;
            StartResearchButton.interactable = canStartMore && hasSelection;
        }

        private static void UpdateResearchDescription(string description)
        {
            CacheResearchUiElements();
            if (ResearchDescriptionText == null)
            {
                return;
            }

            RunOnMainThread(() =>
            {
                if (ResearchDescriptionText != null)
                {
                    ResearchDescriptionText.text = description ?? string.Empty;
                }
            });
        }

        private static async void StartSelectedResearch()
        {
            if (SelectedBuff == null)
            {
                Debug.LogWarning("Select a research buff before starting research.");
                return;
            }

            EnsureCurrentResearchInitialized();
            int maxSimultaneous = Mathf.Max(1, Profile.CurrentResearch.ResearchesAtATime);
            int activeCount = Profile.CurrentResearch.CurrentResearches.Count(buff => !IsBuffComplete(buff));
            if (activeCount >= maxSimultaneous)
            {
                Debug.LogWarning("Maximum concurrent researches reached.");
                UpdateStartResearchButtonState();
                return;
            }

            if (FindActiveCurrentResearchBuff(SelectedBuff.Id) != null)
            {
                Debug.LogWarning("This buff is already being researched.");
                UpdateStartResearchButtonState();
                return;
            }

            Profile.CurrentResearch.CurrentResearches.RemoveAll(buff => buff != null && buff.Id == SelectedBuff.Id);

            Buff clone = CloneBuff(SelectedBuff);
            if (clone == null)
            {
                Debug.LogWarning("Unable to clone buff for research.");
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            clone.ResearchStartTime = utcNow.ToString("o", CultureInfo.InvariantCulture);
            TimeSpan duration = GetDurationFromResearchTime(clone.ResearchTimeToLevelUp);
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromSeconds(1);
            }
            clone.ResearchFinishedTime = utcNow.Add(duration).ToString("o", CultureInfo.InvariantCulture);

            if (activeCount == 0)
            {
                Profile.CurrentResearch.CurrentResearchStarted = utcNow.ToString("o", CultureInfo.InvariantCulture);
                Profile.CurrentResearch.CurrentResearchFinished = string.Empty;
                Profile.CurrentResearch.Research = Research;
            }

            Profile.CurrentResearch.CurrentResearches.Add(clone);
            Profile.CurrentResearch.IsResearching = 1;

            await PersistCurrentResearchAsync();

            ClearSelectedBuff();
            RefreshCurrentResearchButtons();
            UpdateLevelUpButtonVisibility();
            UpdateStartResearchButtonState();
        }

        private static TimeSpan GetDurationFromResearchTime(ResearchTime researchTime)
        {
            if (researchTime == null)
            {
                return TimeSpan.Zero;
            }

            return new TimeSpan(researchTime.days, researchTime.hours, researchTime.minutes, researchTime.seconds);
        }

        private static void RunOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            action();
        }

        private static Buff CloneBuff(Buff source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new Buff
            {
                Id = source.Id,
                BuffName = source.BuffName,
                BuffDescription = source.BuffDescription,
                SpritePath = source.SpritePath,
                ResearchTimeToLevelUp = source.ResearchTimeToLevelUp != null
                    ? new ResearchTime
                    {
                        days = source.ResearchTimeToLevelUp.days,
                        hours = source.ResearchTimeToLevelUp.hours,
                        minutes = source.ResearchTimeToLevelUp.minutes,
                        seconds = source.ResearchTimeToLevelUp.seconds
                    }
                    : new ResearchTime()
            };

            if (source.attributeList != null)
            {
                clone.attributeList = new List<Game.Attribute>(source.attributeList);
            }

            return clone;
        }

        private static void AttachCountdownOverlay(GameObject buttonGameObject, Buff activeBuff)
        {
            if (buttonGameObject == null)
            {
                return;
            }

            Transform overlayTransform = buttonGameObject.transform.Find("CountdownOverlay");
            if (activeBuff == null || string.IsNullOrEmpty(activeBuff.ResearchFinishedTime) || IsBuffComplete(activeBuff))
            {
                if (overlayTransform != null)
                {
                    var countdown = overlayTransform.GetComponent<BuffResearchCountdown>();
                    if (countdown != null)
                    {
                        countdown.enabled = false;
                    }
                    overlayTransform.gameObject.SetActive(false);
                }
                return;
            }

            GameObject overlayObject;
            RectTransform overlayRect;
            if (overlayTransform == null)
            {
                overlayObject = new GameObject("CountdownOverlay", typeof(RectTransform), typeof(Image));
                overlayRect = overlayObject.GetComponent<RectTransform>();
                overlayRect.SetParent(buttonGameObject.transform, false);
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.pivot = new Vector2(0.5f, 0.5f);

                Image overlayImage = overlayObject.GetComponent<Image>();
                overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
                overlayImage.raycastTarget = false;

                GameObject textObject = new GameObject("Text", typeof(RectTransform));
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.SetParent(overlayRect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var tmpText = textObject.AddComponent<TextMeshProUGUI>();
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.color = Color.white;
                tmpText.fontSize = 28f;
                tmpText.raycastTarget = false;

                var countdown = overlayObject.AddComponent<BuffResearchCountdown>();
                countdown.CountdownText = tmpText;
            }
            else
            {
                overlayObject = overlayTransform.gameObject;
                overlayRect = overlayTransform as RectTransform;
            }

            var countdownComponent = overlayObject.GetComponent<BuffResearchCountdown>();
            if (countdownComponent == null)
            {
                var tmp = overlayObject.GetComponentInChildren<TextMeshProUGUI>();
                countdownComponent = overlayObject.AddComponent<BuffResearchCountdown>();
                countdownComponent.CountdownText = tmp;
            }
            else if (countdownComponent.CountdownText == null)
            {
                countdownComponent.CountdownText = overlayObject.GetComponentInChildren<TextMeshProUGUI>();
            }

            countdownComponent.enabled = true;
            countdownComponent.BuffId = activeBuff.Id;
            countdownComponent.StartTimeIso = activeBuff.ResearchStartTime;
            countdownComponent.FinishTimeIso = activeBuff.ResearchFinishedTime;
            countdownComponent.OnComplete = NotifyBuffResearchComplete;
            overlayObject.SetActive(true);
        }

        private static Buff FindActiveCurrentResearchBuff(string buffId)
        {
            if (string.IsNullOrEmpty(buffId) || Profile?.CurrentResearch?.CurrentResearches == null)
            {
                return null;
            }

            return Profile.CurrentResearch.CurrentResearches
                .FirstOrDefault(buff => buff != null && buff.Id == buffId && !IsBuffComplete(buff));
        }

        private static bool IsBuffComplete(Buff buff)
        {
            if (buff == null)
            {
                return true;
            }

            if (string.IsNullOrEmpty(buff.ResearchFinishedTime))
            {
                return false;
            }

            if (!DateTime.TryParseExact(buff.ResearchFinishedTime, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var finish))
            {
                return false;
            }

            return DateTime.UtcNow >= finish;
        }

        private static async Task CleanupCompletedResearchesAsync()
        {
            EnsureCurrentResearchInitialized();
            var list = Profile.CurrentResearch.CurrentResearches;
            if (list == null || list.Count == 0)
            {
                return;
            }

            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (IsBuffComplete(list[i]))
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                bool anyActive = list.Any(buff => !IsBuffComplete(buff));
                Profile.CurrentResearch.IsResearching = anyActive ? 1 : 0;
                if (!anyActive)
                {
                    Profile.CurrentResearch.CurrentResearchFinished = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                }
                await PersistCurrentResearchAsync();
            }
        }

        private static void RefreshCurrentResearchButtons()
        {
            if (ResearchButtonsGameObjectTransform == null || Research == null)
            {
                return;
            }

            if (ResearchBackgroundGameObject != null && !ResearchBackgroundGameObject.activeInHierarchy)
            {
                return;
            }

            MakeButtons(ResearchButtonsGameObjectTransform, false);
        }

        private static async Task PersistCurrentResearchAsync()
        {
            if (Profile?.CurrentResearch == null || Auth.User.user == null)
            {
                return;
            }

            if (DB.Default.GameProfiles == null)
            {
                DB.Default.Init();
            }

            if (DB.Default.GameProfiles == null)
            {
                Debug.LogWarning("GameProfiles collection is not initialised; cannot persist current research.");
                return;
            }

            Profile.CurrentResearch.IsResearching = Profile.CurrentResearch.CurrentResearches
                .Any(buff => !IsBuffComplete(buff)) ? 1 : 0;

            try
            {
                DocumentReference profileRef = DB.Default.GameProfiles.Document(Auth.User.user.UserId);
                var data = new Dictionary<string, object>
                {
                    { "CurrentResearch", Profile.CurrentResearch }
                };
                await profileRef.SetAsync(data, SetOptions.MergeAll);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to persist CurrentResearch: {ex.Message}");
            }
        }

        public static async void NotifyBuffResearchComplete(string buffId)
        {
            if (string.IsNullOrEmpty(buffId))
            {
                return;
            }

            EnsureCurrentResearchInitialized();
            var list = Profile.CurrentResearch.CurrentResearches;
            if (list == null || list.Count == 0)
            {
                return;
            }

            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var buff = list[i];
                if (buff != null && buff.Id == buffId)
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return;
            }

            bool anyActive = list.Any(b => !IsBuffComplete(b));
            Profile.CurrentResearch.IsResearching = anyActive ? 1 : 0;
            if (!anyActive)
            {
                Profile.CurrentResearch.CurrentResearchFinished = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            }
            await PersistCurrentResearchAsync();
            UpdateLevelUpButtonVisibility();
            UpdateStartResearchButtonState();
            RefreshCurrentResearchButtons();
        }

        private static void LogProfileSnapshot(Profile profile, string context)
        {
            if (profile == null)
            {
                Debug.Log($"{context}: Profile snapshot is null");
                return;
            }

            ResearchLevels levels = profile.ResearchLevels ?? new ResearchLevels();
            int activeCount = profile.CurrentResearch?.CurrentResearches?.Count ?? 0;
            Debug.Log($"{context}: Profile snapshot -> Research:{levels.Research}, Base:{levels.Base}, Military:{levels.Military}, Commander:{levels.Comander}, ActiveResearches:{activeCount}");
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite not found at path '{resourcePath}'.");
            }

            return sprite;
        }

        private static string GetNavigationSpritePath(Research.ResearchTypeEnum researchType)
        {
            if (NavigationSpritePaths.TryGetValue(researchType, out string path))
            {
                return path;
            }

            Debug.LogWarning($"No navigation sprite configured for research type {researchType}.");
            return string.Empty;
        }

        private static void WireUpCloseButton()
        {
            if (IsCloseButtonHooked)
            {
                return;
            }

            Transform closeButtonTransform = MainContentGameObject.transform.Find("ResearchBackground/CloseButton");
            if (closeButtonTransform == null)
            {
                Transform backgroundTransform = ResearchBackgroundGameObject != null
                    ? ResearchBackgroundGameObject.transform
                    : MainContentGameObject?.transform.Find("ResearchBackground");
                if (backgroundTransform == null)
                {
                    Debug.LogWarning("Research background transform not found; cannot create close button.");
                    return;
                }

                GameObject closeButtonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                RectTransform rectTransform = closeButtonObject.GetComponent<RectTransform>();
                rectTransform.SetParent(backgroundTransform, false);
                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(-30f, -30f);
                rectTransform.sizeDelta = new Vector2(60f, 60f);

                Image image = closeButtonObject.GetComponent<Image>();
                Sprite sprite = Resources.Load<Sprite>("UI/default_close_button");
                image.sprite = sprite;
                image.color = new Color(0.85f, 0.25f, 0.25f, 0.9f);

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(rectTransform, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                Text labelText = labelObject.GetComponent<Text>();
                labelText.text = "X";
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = Color.white;
                labelText.fontStyle = FontStyle.Bold;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 12;
                labelText.resizeTextMaxSize = 48;

                closeButtonTransform = closeButtonObject.transform;
            }

            CloseButtonGameObject = closeButtonTransform.gameObject;
            CloseButton = CloseButtonGameObject.GetComponent<Button>();
            if (CloseButton == null)
            {
                CloseButton = CloseButtonGameObject.AddComponent<Button>();
            }
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(CloseResearchBackground);
            IsCloseButtonHooked = true;
        }

        private static void WireUpStartResearchButton()
        {
            if (StartResearchButton == null)
            {
                return;
            }

            StartResearchButton.onClick.RemoveAllListeners();
            StartResearchButton.onClick.AddListener(StartSelectedResearch);
        }

        private static void CloseResearchBackground()
        {
            ShowResearchBackground(false);
            if (ResearchButtonsGameObjectTransform != null)
            {
                DeleteAllChildren(ResearchButtonsGameObjectTransform);
            }
            if (MainNavigationGameObjectTransform != null)
            {
                MakeButtons(MainNavigationGameObjectTransform, true);
            }
            CurrentButtonClicked = null;
        }

        public static void DeleteAllChildren(Transform parent) {
            if (parent == null)
            {
                return;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                ButtonBuffLookup.Remove(child);
                GameObject.Destroy(child);
            }
        }

    }
}
