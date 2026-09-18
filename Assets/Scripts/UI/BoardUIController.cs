using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Janggi.Core;
using Janggi.AI;

namespace Janggi.UI
{
    public enum CalloutType
    {
        Check,  // 장군 (將軍)
        Escape  // 멍군 (應將)
    }

    /// <summary>
    /// UI Toolkit 기반 장기판 UI 컨트롤러.
    /// Board 로직, 자원(Cost), 손패(Hand), 소환(Spawn) 구역 렌더링, 다국어 지원 및 이벤트를 총괄합니다.
    /// </summary>
    public class BoardUIController
    {
        private const float BoardAspectRatio = 9f / 10f;
        private const float PieceFontRatio = 0.045f;

        private readonly VisualElement _root;
        private readonly VisualElement _boardArea;
        private readonly VisualElement _boardContainer;
        private readonly VisualElement _gridBackground;
        private readonly VisualElement _intersectionsGrid;
        private readonly VisualElement _pieceLayer;
        private readonly VisualElement _dangerVignette;
        private readonly Dictionary<Piece, VisualElement> _pieceElements = new Dictionary<Piece, VisualElement>();
        private readonly Dictionary<Piece, BoardPosition> _pieceLastPositions = new Dictionary<Piece, BoardPosition>();
        private readonly VisualElement _calloutBanner;
        private readonly Label _calloutHanja;
        private readonly Label _calloutSubtitle;
        private IVisualElementScheduledItem _hideCalloutSchedule;
        private readonly VisualElement _hanTurnInfo;
        private readonly VisualElement _choTurnInfo;
        private readonly Label _hanTurnLabel;
        private readonly Label _choTurnLabel;
        private readonly Label _hanStatusLabel;
        private readonly Label _choStatusLabel;

        // 플레이어/AI 패널 및 손패 슬롯
        private readonly Label _hanCostLabel;
        private readonly Label _choCostLabel;
        private readonly Label _hanFieldCostLabel;
        private readonly Label _choFieldCostLabel;
        private readonly Label _hanSideLabel;
        private readonly Label _choSideLabel;
        private readonly VisualElement _hanHandContainer;
        private readonly VisualElement _choHandContainer;
        private readonly Button _btnDiscardMode;
        private readonly Button _btnDifficulty;

        // 게임 오버 인라인 결과창 UI (보드 아래 배치)
        private readonly VisualElement _gameOverInlineArea;
        private readonly Label _inlineModalTitle;
        private readonly Label _inlineModalBadge;
        private readonly VisualElement _inlineReviewSection;
        private readonly Label _inlineReviewDesc;
        private readonly Label _statLabelDifficulty;
        private readonly Label _statDifficulty;
        private readonly Label _statLabelTurns;
        private readonly Label _statTurns;
        private readonly Label _statLabelCaptures;
        private readonly Label _statCaptures;
        private readonly Label _statLabelSummons;
        private readonly Label _statSummons;
        private readonly Button _btnModalRestart;
        private readonly Button _btnModalMenu;
        private BoardReviewData _lastReviewData;

        // 교차점 버튼 배열 [col, row]
        private readonly VisualElement[,] _intersectionElements;

        // 현재 게임 상태 참조
        private Board _board;
        private PlayerState _choState;
        private PlayerState _hanState;
        private PlayerSide _currentTurn;
        private bool _isInteractive = true;

        // 마지막 모달 상태 저장 (언어 전환 시 즉시 갱신용)
        private bool _isModalShown = false;
        private bool _lastModalIsWin;
        private bool _lastModalIsDraw;
        private AIDifficulty _lastModalDiff;
        private int _lastModalTurns;
        private int _lastModalCaptures;
        private int _lastModalSummons;

        // 선택 상태
        private Piece _selectedPiece;
        private List<BoardPosition> _highlightedMoves;
        private BoardPosition? _lastMoveFrom;
        private BoardPosition? _lastMoveTo;

        // 화면 컨테이너
        private readonly VisualElement _mainMenuScreen;
        private readonly VisualElement _gamePlayScreen;

        // 메인 메뉴 요소
        private readonly Label _menuTitleSub;
        private readonly Label _menuSubtitle;
        private readonly Label _menuDiffSectionTitle;
        private readonly VisualElement _diffCardEasy;
        private readonly VisualElement _diffCardNormal;
        private readonly VisualElement _diffCardHard;
        private readonly VisualElement _diffCardHell;
        private readonly Label _diffEasyTitle;
        private readonly Label _diffEasyDesc;
        private readonly Label _diffNormalTitle;
        private readonly Label _diffNormalDesc;
        private readonly Label _diffHardTitle;
        private readonly Label _diffHardDesc;
        private readonly Label _diffHellTitle;
        private readonly Label _diffHellDesc;

        private readonly Button _btnStartGame;
        private readonly Button _btnOpenRules;
        private readonly Button _btnHapticToggle;
        private readonly Button _btnLanguageToggle;
        private readonly Button _btnCloseRules;
        private readonly VisualElement _ruleGuideModal;
        private readonly Label _ruleModalTitle;
        private readonly Label _rule1Title;
        private readonly Label _rule1Desc;
        private readonly Label _rule2Title;
        private readonly Label _rule2Desc;
        private readonly Label _rule3Title;
        private readonly Label _rule3Desc;
        private readonly Label _rule4Title;
        private readonly Label _rule4Desc;
        private readonly Label _rule5Title;
        private readonly Label _rule5Desc;
        private readonly Label _pauseModalTitle;

        private AIDifficulty _selectedDifficulty = AIDifficulty.Normal;

        // 인게임 헤더 및 버튼
        private readonly Label _gameTitle;
        private readonly Button _btnHeaderHaptic;
        private readonly Button _btnHeaderLanguage;
        private readonly Button _btnGotoMenu;
        private readonly Button _btnNewGame;
        private readonly Button _btnPassTurn;
        private readonly Button _btnAdChance;
        
        // 일시정지 모달 및 버튼
        private readonly Button _btnPause;
        private readonly VisualElement _pauseModal;
        private readonly Button _btnContinue;

        // 광고 로딩 및 시청 모달 요소
        private readonly VisualElement _adLoadingOverlay;
        private readonly Label _adLoadingText;

        // 광고 시청 모달 요소
        private readonly VisualElement _adViewModal;
        private readonly VisualElement _adProgressBarFill;
        private readonly Label _adModalStatus;

        // 소환 및 찬스 선택 상태
        private int _selectedHandCardIndex = -1;
        private List<BoardPosition> _highlightedSpawnPositions;
        private bool _isDiscardMode = false;
        private bool _isEliminateMode = false;
        private List<BoardPosition> _eliminateTargets;
        private bool _hasUsedAdChance = false;
        private float _cachedBoardWidth = 0f;

        // 이벤트 콜백
        public System.Action<Piece> OnPieceSelected;
        public System.Action<Piece, BoardPosition> OnMoveRequested;
        public System.Action<int, BoardPosition> OnSpawnRequested; // handIndex, spawnPos
        public System.Action<int> OnDiscardRequested;              // handIndex
        public System.Action OnDifficultyToggled;
        public System.Action OnRestartRequested;
        public System.Action<AIDifficulty> OnStartGameRequested;
        public System.Action OnReturnToMenuRequested;
        public System.Action OnPassRequested;
        public System.Action OnAdChanceRequested;
        public System.Action<Piece> OnEliminateTargetSelected;

        public BoardUIController(VisualElement root)
        {
            _root = root;

            // 1. 화면 컨테이너 바인딩
            _mainMenuScreen = root.Q<VisualElement>("main-menu-screen");
            _gamePlayScreen = root.Q<VisualElement>("game-play-screen");

            // 2. 메인 메뉴 요소 바인딩
            _menuTitleSub = root.Q<Label>("menu-title-sub");
            _menuSubtitle = root.Q<Label>("menu-subtitle");
            _menuDiffSectionTitle = root.Q<Label>("menu-diff-section-title");

            _diffCardEasy = root.Q<VisualElement>("diff-card-easy");
            _diffCardNormal = root.Q<VisualElement>("diff-card-normal");
            _diffCardHard = root.Q<VisualElement>("diff-card-hard");
            _diffCardHell = root.Q<VisualElement>("diff-card-hell");

            _diffEasyTitle = root.Q<Label>("diff-easy-title");
            _diffEasyDesc = root.Q<Label>("diff-easy-desc");
            _diffNormalTitle = root.Q<Label>("diff-normal-title");
            _diffNormalDesc = root.Q<Label>("diff-normal-desc");
            _diffHardTitle = root.Q<Label>("diff-hard-title");
            _diffHardDesc = root.Q<Label>("diff-hard-desc");
            _diffHellTitle = root.Q<Label>("diff-hell-title");
            _diffHellDesc = root.Q<Label>("diff-hell-desc");

            _btnStartGame = root.Q<Button>("btn-start-game");
            _btnOpenRules = root.Q<Button>("btn-open-rules");
            _btnLanguageToggle = root.Q<Button>("btn-language-toggle");
            _btnCloseRules = root.Q<Button>("btn-close-rules");
            _ruleGuideModal = root.Q<VisualElement>("rule-guide-modal");

            _ruleModalTitle = root.Q<Label>("rule-modal-title");
            _rule1Title = root.Q<Label>("rule-1-title");
            _rule1Desc = root.Q<Label>("rule-1-desc");
            _rule2Title = root.Q<Label>("rule-2-title");
            _rule2Desc = root.Q<Label>("rule-2-desc");
            _rule3Title = root.Q<Label>("rule-3-title");
            _rule3Desc = root.Q<Label>("rule-3-desc");
            _rule4Title = root.Q<Label>("rule-4-title");
            _rule4Desc = root.Q<Label>("rule-4-desc");
            _rule5Title = root.Q<Label>("rule-5-title");
            _rule5Desc = root.Q<Label>("rule-5-desc");

            // 3. 인게임 요소 바인딩
            _boardArea = root.Q<VisualElement>("board-area");
            _boardContainer = root.Q<VisualElement>("board-container");
            _gridBackground = root.Q<VisualElement>("grid-background");
            _intersectionsGrid = root.Q<VisualElement>("intersections-grid");
            _pieceLayer = root.Q<VisualElement>("piece-layer");
            _pieceLayer.pickingMode = PickingMode.Ignore;
            _dangerVignette = root.Q<VisualElement>("danger-vignette");
            if (_dangerVignette != null) _dangerVignette.pickingMode = PickingMode.Ignore;
            _calloutBanner = root.Q<VisualElement>("callout-banner");
            _calloutHanja = root.Q<Label>("callout-hanja");
            _calloutSubtitle = root.Q<Label>("callout-subtitle");

            _hanTurnInfo = root.Q<VisualElement>("han-turn-info");
            _choTurnInfo = root.Q<VisualElement>("cho-turn-info");
            _hanTurnLabel = root.Q<Label>("han-turn-label");
            _choTurnLabel = root.Q<Label>("cho-turn-label");
            _hanStatusLabel = root.Q<Label>("han-status-label");
            _choStatusLabel = root.Q<Label>("cho-status-label");

            _hanSideLabel = root.Q<Label>("han-side-label");
            _choSideLabel = root.Q<Label>("cho-side-label");
            _gameTitle = root.Q<Label>("game-title");
            _hanCostLabel = root.Q<Label>("han-cost-label");
            _choCostLabel = root.Q<Label>("cho-cost-label");
            _hanFieldCostLabel = root.Q<Label>("han-field-cost-label");
            _choFieldCostLabel = root.Q<Label>("cho-field-cost-label");
            _hanHandContainer = root.Q<VisualElement>("han-hand-container");
            _choHandContainer = root.Q<VisualElement>("cho-hand-container");
            _btnDiscardMode = root.Q<Button>("btn-discard-mode");
            _btnPassTurn = root.Q<Button>("btn-pass-turn");
            _btnAdChance = root.Q<Button>("btn-ad-chance");
            _btnDifficulty = root.Q<Button>("btn-difficulty");
            _btnHeaderHaptic = root.Q<Button>("btn-header-haptic");
            _btnHeaderLanguage = root.Q<Button>("btn-header-language");
            _btnNewGame = root.Q<Button>("btn-new-game");
            _btnGotoMenu = root.Q<Button>("btn-goto-menu");
            _btnHapticToggle = root.Q<Button>("btn-haptic-toggle");
            
            _btnPause = root.Q<Button>("btn-pause");
            _pauseModal = root.Q<VisualElement>("pause-modal");
            _pauseModalTitle = root.Q<Label>("pause-modal-title");
            _btnContinue = root.Q<Button>("btn-continue");

            _adLoadingOverlay = root.Q<VisualElement>("ad-loading-overlay");
            _adLoadingText = root.Q<Label>("ad-loading-text");

            // 4. 인라인 게임 오버 결과창 바인딩
            _gameOverInlineArea = root.Q<VisualElement>("game-over-inline-area");
            _inlineModalTitle = root.Q<Label>("inline-modal-title");
            _inlineModalBadge = root.Q<Label>("inline-modal-badge");
            _inlineReviewSection = root.Q<VisualElement>("inline-review-section");
            _inlineReviewDesc = root.Q<Label>("inline-review-desc");
            _statLabelDifficulty = root.Q<Label>("stat-label-difficulty");
            _statDifficulty = root.Q<Label>("stat-difficulty");
            _statLabelTurns = root.Q<Label>("stat-label-turns");
            _statTurns = root.Q<Label>("stat-turns");
            _statLabelCaptures = root.Q<Label>("stat-label-captures");
            _statCaptures = root.Q<Label>("stat-captures");
            _statLabelSummons = root.Q<Label>("stat-label-summons");
            _statSummons = root.Q<Label>("stat-summons");
            _btnModalRestart = root.Q<Button>("btn-modal-restart");
            _btnModalMenu = root.Q<Button>("btn-modal-menu");

            _intersectionElements = new VisualElement[BoardPosition.MaxCol, BoardPosition.MaxRow];
            _highlightedMoves = new List<BoardPosition>();
            _highlightedSpawnPositions = new List<BoardPosition>();
            _eliminateTargets = new List<BoardPosition>();

            BuildGridLines();
            BuildGrid();
            SetupButtons();
            SetupMainMenu();

            // 다국어 초기 텍스트 적용 및 이벤트 구독
            ApplyLocalization();
            LocalizationManager.OnLanguageChanged += ApplyLocalization;
            MobileHapticManager.Instance.OnHapticSettingChanged += OnHapticSettingChanged;
            ApplyDifficultyTheme(_selectedDifficulty);

            // 반응형 보드 크기 계산
            if (_boardArea != null)
            {
                _boardArea.RegisterCallback<GeometryChangedEvent>(OnBoardAreaGeometryChanged);
            }

            // Safe Area(노치/카메라홀/상태바) 동적 패딩 적용 (헤더/UI가 가려지지 않도록 보호)
            ApplySafeArea();
            _root.RegisterCallback<GeometryChangedEvent>(evt => ApplySafeArea());
        }

        private void BuildGridLines()
        {
            if (_gridBackground == null) return;
            _gridBackground.Clear();

            // 1. 가로선 10줄 (Row 0 ~ Row 9 중심)
            for (int row = 0; row < BoardPosition.MaxRow; row++)
            {
                var hLine = new VisualElement();
                hLine.AddToClassList("grid-line-h");
                // row 9(상단) ~ row 0(하단)
                float topPercent = (BoardPosition.MaxRow - 1 - row + 0.5f) * 10f;
                hLine.style.top = Length.Percent(topPercent);
                _gridBackground.Add(hLine);
            }

            // 2. 세로선 9줄 (Col 0 ~ Col 8 중심)
            float colStep = 100f / BoardPosition.MaxCol;
            for (int col = 0; col < BoardPosition.MaxCol; col++)
            {
                var vLine = new VisualElement();
                vLine.AddToClassList("grid-line-v");
                float leftPercent = (col + 0.5f) * colStep;
                vLine.style.left = Length.Percent(leftPercent);
                _gridBackground.Add(vLine);
            }
        }

        private void SetupButtons()
        {
            if (_btnDiscardMode != null)
            {
                _btnDiscardMode.clicked += ToggleDiscardMode;
            }

            if (_btnPassTurn != null)
            {
                _btnPassTurn.clicked += () => OnPassRequested?.Invoke();
            }

            if (_btnAdChance != null)
            {
                _btnAdChance.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(evt => 
                {
                    if (_btnAdChance.enabledSelf)
                    {
                        OnAdChanceRequested?.Invoke();
                        evt.StopPropagation();
                    }
                }, TrickleDown.TrickleDown);
            }

            if (_btnDifficulty != null)
            {
                _btnDifficulty.clicked += () => OnDifficultyToggled?.Invoke();
            }

            if (_btnHeaderHaptic != null)
            {
                _btnHeaderHaptic.clicked += () =>
                {
                    MobileHapticManager.Instance.ToggleHaptic();
                    UpdateHapticButtonUI();
                };
            }

            if (_btnHeaderLanguage != null)
            {
                _btnHeaderLanguage.clicked += () => LocalizationManager.ToggleLanguage();
            }

            if (_btnNewGame != null)
            {
                _btnNewGame.clicked += () => 
                {
                    if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.None;
                    OnRestartRequested?.Invoke();
                };
            }

            if (_btnGotoMenu != null)
            {
                _btnGotoMenu.clicked += () => 
                {
                    if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.None;
                    OnReturnToMenuRequested?.Invoke();
                };
            }

            if (_btnModalRestart != null)
            {
                _btnModalRestart.clicked += () =>
                {
                    HideGameOverModal();
                    OnRestartRequested?.Invoke();
                };
            }

            if (_btnModalMenu != null)
            {
                _btnModalMenu.clicked += () =>
                {
                    HideGameOverModal();
                    OnReturnToMenuRequested?.Invoke();
                };
            }

            if (_btnPause != null)
            {
                _btnPause.clicked += () => 
                {
                    if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.Flex;
                };
            }

            if (_btnContinue != null)
            {
                _btnContinue.clicked += () => 
                {
                    if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.None;
                };
            }
        }

        private void SetupMainMenu()
        {
            // 1. 난이도 카드 클릭 이벤트
            _diffCardEasy?.RegisterCallback<ClickEvent>(evt => SelectDifficulty(AIDifficulty.Easy));
            _diffCardNormal?.RegisterCallback<ClickEvent>(evt => SelectDifficulty(AIDifficulty.Normal));
            _diffCardHard?.RegisterCallback<ClickEvent>(evt => SelectDifficulty(AIDifficulty.Hard));
            _diffCardHell?.RegisterCallback<ClickEvent>(evt => SelectDifficulty(AIDifficulty.Hell));

            // 2. 게임 시작 버튼
            if (_btnStartGame != null)
            {
                _btnStartGame.clicked += () =>
                {
                    OnStartGameRequested?.Invoke(_selectedDifficulty);
                };
            }

            // 3. 게임 방법 버튼 & 닫기 버튼
            if (_btnOpenRules != null)
            {
                _btnOpenRules.clicked += () =>
                {
                    if (_ruleGuideModal != null) _ruleGuideModal.style.display = DisplayStyle.Flex;
                };
            }

            if (_btnLanguageToggle != null)
            {
                _btnLanguageToggle.clicked += () => LocalizationManager.ToggleLanguage();
            }

            if (_btnHapticToggle != null)
            {
                _btnHapticToggle.clicked += () =>
                {
                    MobileHapticManager.Instance.ToggleHaptic();
                    UpdateHapticButtonUI();
                };
            }

            if (_btnCloseRules != null)
            {
                _btnCloseRules.clicked += () =>
                {
                    if (_ruleGuideModal != null) _ruleGuideModal.style.display = DisplayStyle.None;
                };
            }

            // 초기 난이도 선택 상태 동기화
            SelectDifficulty(_selectedDifficulty);
        }

        private void OnHapticSettingChanged(bool _)
        {
            UpdateHapticButtonUI();
        }

        public void UpdateHapticButtonUI()
        {
            bool isEnabled = MobileHapticManager.Instance.IsHapticEnabled;
            if (_btnHapticToggle != null && _btnHapticToggle.panel != null)
            {
                _btnHapticToggle.text = isEnabled ? LocalizationManager.Get("Btn_Haptic_On") : LocalizationManager.Get("Btn_Haptic_Off");
            }
            if (_btnHeaderHaptic != null && _btnHeaderHaptic.panel != null)
            {
                _btnHeaderHaptic.text = _btnHapticToggle.text;
            }
        }

        /// <summary>
        /// BoardUIController 리소스 및 이벤트 구독을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            LocalizationManager.OnLanguageChanged -= ApplyLocalization;
            MobileHapticManager.Instance.OnHapticSettingChanged -= OnHapticSettingChanged;

            if (_boardArea != null)
            {
                _boardArea.UnregisterCallback<GeometryChangedEvent>(OnBoardAreaGeometryChanged);
            }
        }

        /// <summary>
        /// 언어 설정 변경 시 모든 정적 및 동적 UI 텍스트를 즉시 갱신합니다.
        /// </summary>
        public void ApplyLocalization()
        {
            try
            {
                // 1. 메인 메뉴
                if (_menuTitleSub != null && _menuTitleSub.panel != null) _menuTitleSub.text = LocalizationManager.Get("Menu_Title_Sub");
                if (_menuSubtitle != null && _menuSubtitle.panel != null) _menuSubtitle.text = LocalizationManager.Get("Menu_Subtitle");
                if (_menuDiffSectionTitle != null && _menuDiffSectionTitle.panel != null) _menuDiffSectionTitle.text = LocalizationManager.Get("Menu_Diff_Section");

                if (_diffEasyTitle != null && _diffEasyTitle.panel != null) _diffEasyTitle.text = LocalizationManager.Get("Diff_Easy_Title");
                if (_diffEasyDesc != null && _diffEasyDesc.panel != null) _diffEasyDesc.text = LocalizationManager.Get("Diff_Easy_Desc");
                if (_diffNormalTitle != null && _diffNormalTitle.panel != null) _diffNormalTitle.text = LocalizationManager.Get("Diff_Normal_Title");
                if (_diffNormalDesc != null && _diffNormalDesc.panel != null) _diffNormalDesc.text = LocalizationManager.Get("Diff_Normal_Desc");
                if (_diffHardTitle != null && _diffHardTitle.panel != null) _diffHardTitle.text = LocalizationManager.Get("Diff_Hard_Title");
                if (_diffHardDesc != null && _diffHardDesc.panel != null) _diffHardDesc.text = LocalizationManager.Get("Diff_Hard_Desc");
                if (_diffHellTitle != null && _diffHellTitle.panel != null) _diffHellTitle.text = LocalizationManager.Get("Diff_Hell_Title");
                if (_diffHellDesc != null && _diffHellDesc.panel != null) _diffHellDesc.text = LocalizationManager.Get("Diff_Hell_Desc");

                if (_btnStartGame != null && _btnStartGame.panel != null) _btnStartGame.text = LocalizationManager.Get("Btn_Start_Game");
                if (_btnOpenRules != null && _btnOpenRules.panel != null) _btnOpenRules.text = LocalizationManager.Get("Btn_Open_Rules");
                if (_btnLanguageToggle != null && _btnLanguageToggle.panel != null) _btnLanguageToggle.text = LocalizationManager.GetLanguageToggleLabel();
                if (_btnCloseRules != null && _btnCloseRules.panel != null) _btnCloseRules.text = LocalizationManager.Get("Btn_Close_Rules");

                // 2. 규칙 모달
                if (_ruleModalTitle != null && _ruleModalTitle.panel != null) _ruleModalTitle.text = LocalizationManager.Get("Rule_Modal_Title");
                if (_rule1Title != null && _rule1Title.panel != null) _rule1Title.text = LocalizationManager.Get("Rule_1_Title");
                if (_rule1Desc != null && _rule1Desc.panel != null) _rule1Desc.text = LocalizationManager.Get("Rule_1_Desc");
                if (_rule2Title != null && _rule2Title.panel != null) _rule2Title.text = LocalizationManager.Get("Rule_2_Title");
                if (_rule2Desc != null && _rule2Desc.panel != null) _rule2Desc.text = LocalizationManager.Get("Rule_2_Desc");
                if (_rule3Title != null && _rule3Title.panel != null) _rule3Title.text = LocalizationManager.Get("Rule_3_Title");
                if (_rule3Desc != null && _rule3Desc.panel != null) _rule3Desc.text = LocalizationManager.Get("Rule_3_Desc");
                if (_rule4Title != null && _rule4Title.panel != null) _rule4Title.text = LocalizationManager.Get("Rule_4_Title");
                if (_rule4Desc != null && _rule4Desc.panel != null) _rule4Desc.text = LocalizationManager.Get("Rule_4_Desc");
                if (_rule5Title != null && _rule5Title.panel != null) _rule5Title.text = LocalizationManager.Get("Rule_5_Title");
                if (_rule5Desc != null && _rule5Desc.panel != null) _rule5Desc.text = LocalizationManager.Get("Rule_5_Desc");

                // 3. 인게임 헤더 및 패널
                if (_gameTitle != null && _gameTitle.panel != null) _gameTitle.text = LocalizationManager.Get("Header_Title");
                if (_btnHeaderLanguage != null && _btnHeaderLanguage.panel != null) _btnHeaderLanguage.text = LocalizationManager.GetHeaderLanguageToggleLabel();
                if (_btnNewGame != null && _btnNewGame.panel != null) _btnNewGame.text = LocalizationManager.Get("Btn_Restart");
                if (_btnGotoMenu != null && _btnGotoMenu.panel != null) _btnGotoMenu.text = LocalizationManager.Get("Btn_Menu");
                if (_btnPassTurn != null && _btnPassTurn.panel != null) _btnPassTurn.text = LocalizationManager.Get("Btn_Pass");
                if (_btnPause != null && _btnPause.panel != null) _btnPause.text = LocalizationManager.Get("Btn_Pause");
                
                if (_pauseModalTitle != null && _pauseModalTitle.panel != null) _pauseModalTitle.text = LocalizationManager.Get("Modal_Pause_Title");
                if (_btnContinue != null && _btnContinue.panel != null) _btnContinue.text = LocalizationManager.Get("Btn_Continue");

                if (_hanSideLabel != null && _hanSideLabel.panel != null) _hanSideLabel.text = LocalizationManager.Get("Side_Han");
                if (_choSideLabel != null && _choSideLabel.panel != null) _choSideLabel.text = LocalizationManager.Get("Side_Cho");

                UpdateDifficultyDisplay(_selectedDifficulty);
                UpdateDiscardButtonUI();
                UpdateHapticButtonUI();
                SetAdChanceButtonState(_hasUsedAdChance, true);

                // 4. 모달 라벨들
                if (_statLabelDifficulty != null && _statLabelDifficulty.panel != null) _statLabelDifficulty.text = LocalizationManager.Get("Stat_Difficulty");
                if (_statLabelTurns != null && _statLabelTurns.panel != null) _statLabelTurns.text = LocalizationManager.Get("Stat_Turns");
                if (_statLabelCaptures != null && _statLabelCaptures.panel != null) _statLabelCaptures.text = LocalizationManager.Get("Stat_Captures");
                if (_statLabelSummons != null && _statLabelSummons.panel != null) _statLabelSummons.text = LocalizationManager.Get("Stat_Summons");

                if (_btnModalRestart != null && _btnModalRestart.panel != null) _btnModalRestart.text = LocalizationManager.Get("Btn_Modal_Restart");
                if (_btnModalMenu != null && _btnModalMenu.panel != null) _btnModalMenu.text = LocalizationManager.Get("Btn_Modal_Menu");

                if (_adLoadingText != null && _adLoadingText.panel != null) _adLoadingText.text = LocalizationManager.Get("Msg_Ad_Loading");

                if (_isModalShown)
                {
                    ShowGameOverModal(_lastModalIsWin, _lastModalIsDraw, _lastModalDiff, _lastModalTurns, _lastModalCaptures, _lastModalSummons, _lastReviewData);
                }

                // 5. 인게임 동적 UI 갱신
                RefreshAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoardUIController] ApplyLocalization 중 예외 무시: {ex.Message}");
            }
        }

        public void SelectDifficulty(AIDifficulty difficulty)
        {
            _selectedDifficulty = difficulty;

            _diffCardEasy?.parent?.RemoveFromClassList("diff-card-wrapper--selected");
            _diffCardNormal?.parent?.RemoveFromClassList("diff-card-wrapper--selected");
            _diffCardHard?.parent?.RemoveFromClassList("diff-card-wrapper--selected");
            _diffCardHell?.parent?.RemoveFromClassList("diff-card-wrapper--selected");

            VisualElement selectedCard = difficulty switch
            {
                AIDifficulty.Easy => _diffCardEasy,
                AIDifficulty.Normal => _diffCardNormal,
                AIDifficulty.Hard => _diffCardHard,
                AIDifficulty.Hell => _diffCardHell,
                _ => null
            };

            selectedCard?.parent?.AddToClassList("diff-card-wrapper--selected");

            UpdateDifficultyDisplay(difficulty);
        }

        public void ShowMainMenu()
        {
            if (_mainMenuScreen != null) _mainMenuScreen.style.display = DisplayStyle.Flex;
            if (_gamePlayScreen != null) _gamePlayScreen.style.display = DisplayStyle.None;
            if (_ruleGuideModal != null) _ruleGuideModal.style.display = DisplayStyle.None;
            HideGameOverModal();
        }

        public void ShowGamePlay()
        {
            if (_mainMenuScreen != null) _mainMenuScreen.style.display = DisplayStyle.None;
            if (_gamePlayScreen != null) _gamePlayScreen.style.display = DisplayStyle.Flex;
        }

        public void ShowGameOverModal(bool isWin, bool isDraw, AIDifficulty difficulty, int turnCount, int captures, int summons, BoardReviewData reviewData = null)
        {
            _isModalShown = true;
            _lastModalIsWin = isWin;
            _lastModalIsDraw = isDraw;
            _lastModalDiff = difficulty;
            _lastModalTurns = turnCount;
            _lastModalCaptures = captures;
            _lastModalSummons = summons;
            _lastReviewData = reviewData;

            // 상하 패널 및 액션 바를 숨기고 보드를 상단에 배치
            _gamePlayScreen?.AddToClassList("state--game-over");

            if (_gameOverInlineArea != null)
            {
                _gameOverInlineArea.style.display = DisplayStyle.Flex;
            }

            if (_inlineModalTitle != null)
            {
                _inlineModalTitle.RemoveFromClassList("inline-modal-title--win");
                _inlineModalTitle.RemoveFromClassList("inline-modal-title--lose");
                _inlineModalTitle.RemoveFromClassList("inline-modal-title--draw");

                if (isDraw)
                {
                    _inlineModalTitle.text = LocalizationManager.Get("Modal_Title_Draw");
                    _inlineModalTitle.AddToClassList("inline-modal-title--draw");
                    if (_inlineModalBadge != null) _inlineModalBadge.text = LocalizationManager.Get("Badge_Stalemate");
                }
                else if (isWin)
                {
                    _inlineModalTitle.text = LocalizationManager.Get("Modal_Title_Win");
                    _inlineModalTitle.AddToClassList("inline-modal-title--win");
                    if (_inlineModalBadge != null) _inlineModalBadge.text = LocalizationManager.Get("Badge_Checkmate_Win");
                }
                else
                {
                    _inlineModalTitle.text = LocalizationManager.Get("Modal_Title_Lose");
                    _inlineModalTitle.AddToClassList("inline-modal-title--lose");
                    if (_inlineModalBadge != null) _inlineModalBadge.text = LocalizationManager.Get("Badge_Checkmate_Lose");
                }
            }

            // 외통수 분석 텍스트 채우기 및 보드 하이라이트 표시
            if (reviewData != null)
            {
                reviewData.RebuildExplanation();
                if (_inlineReviewSection != null) _inlineReviewSection.style.display = DisplayStyle.Flex;
                if (_inlineReviewDesc != null) _inlineReviewDesc.text = reviewData.Explanation;
                RenderReviewHighlights(reviewData);
            }
            else if (_inlineReviewSection != null)
            {
                _inlineReviewSection.style.display = DisplayStyle.None;
            }

            if (_statDifficulty != null) _statDifficulty.text = difficulty.GetDisplayName();
            if (_statTurns != null) _statTurns.text = LocalizationManager.Get("Stat_Turns_Value", turnCount);
            if (_statCaptures != null) _statCaptures.text = LocalizationManager.Get("Stat_Captures_Value", captures);
            if (_statSummons != null) _statSummons.text = LocalizationManager.Get("Stat_Summons_Value", summons);
        }

        public void HideGameOverModal()
        {
            _isModalShown = false;
            _gamePlayScreen?.RemoveFromClassList("state--game-over");
            if (_gameOverInlineArea != null)
            {
                _gameOverInlineArea.style.display = DisplayStyle.None;
            }
            ClearReviewHighlights();
        }

        private void RenderReviewHighlights(BoardReviewData data)
        {
            ClearReviewHighlights();

            // 1. 직접 공격 기물들 (Direct Attackers)
            foreach (var atk in data.DirectAttackers)
            {
                if (atk.Position.IsValid())
                {
                    _intersectionElements[atk.Position.Col, atk.Position.Row]?.AddToClassList("intersection--review-attacker");
                }
            }

            // 2. 경로 차단 기물들 (Path Controllers)
            foreach (var ctrl in data.PathControllers)
            {
                if (ctrl.Position.IsValid())
                {
                    _intersectionElements[ctrl.Position.Col, ctrl.Position.Row]?.AddToClassList("intersection--review-controller");
                }
            }

            // 3. 차단된 왕의 퇴로 위치들
            foreach (var pos in data.BlockedEscapePositions)
            {
                if (pos.IsValid())
                {
                    _intersectionElements[pos.Col, pos.Row]?.AddToClassList("intersection--review-blocked-pos");
                }
            }

            // 4. 외통수당한 왕 (Target King)
            if (data.KingPiece != null && data.KingPiece.Position.IsValid())
            {
                _intersectionElements[data.KingPiece.Position.Col, data.KingPiece.Position.Row]?.AddToClassList("intersection--review-target-king");
            }
        }

        private void ClearReviewHighlights()
        {
            for (int col = 0; col < BoardPosition.MaxCol; col++)
            {
                for (int row = 0; row < BoardPosition.MaxRow; row++)
                {
                    var elem = _intersectionElements[col, row];
                    if (elem != null)
                    {
                        elem.RemoveFromClassList("intersection--review-attacker");
                        elem.RemoveFromClassList("intersection--review-controller");
                        elem.RemoveFromClassList("intersection--review-target-king");
                        elem.RemoveFromClassList("intersection--review-blocked-pos");
                    }
                }
            }
        }

        /// <summary>
        /// 광고 시청 화면을 1.2초간 시각적으로 표시하고 완료 콜백을 실행합니다.
        /// </summary>
        public void ShowAdPlaybackModal(System.Action onFinished)
        {
            if (_adViewModal == null)
            {
                onFinished?.Invoke();
                return;
            }

            _adViewModal.style.display = DisplayStyle.Flex;
            if (_adProgressBarFill != null) _adProgressBarFill.style.width = Length.Percent(0);
            if (_adModalStatus != null) _adModalStatus.text = "광고 재생 중... (1.5s)";

            // 프로그레스 바 차오름 연출 시작
            _adViewModal.schedule.Execute(() =>
            {
                if (_adProgressBarFill != null) _adProgressBarFill.style.width = Length.Percent(100);
            }).StartingIn(50);

            // 1.2초 후 완료 및 닫기
            _adViewModal.schedule.Execute(() =>
            {
                if (_adModalStatus != null) _adModalStatus.text = "보상 지급 완료!";

                _adViewModal.schedule.Execute(() =>
                {
                    _adViewModal.style.display = DisplayStyle.None;
                    if (_adProgressBarFill != null) _adProgressBarFill.style.width = Length.Percent(0);
                    onFinished?.Invoke();
                }).StartingIn(300);
            }).StartingIn(1200);
        }

        /// <summary>
        /// 모바일 기기의 노치/카메라 홀/상태바(Safe Area)에 헤더 및 UI가 겹치지 않도록 안전 영역 패딩을 동적으로 적용합니다.
        /// </summary>
        public void ApplySafeArea()
        {
            if (_root == null) return;

            var rootContainer = _root.Q<VisualElement>("root-container") ?? _root;
            if (rootContainer == null) return;

            Rect safeArea = Screen.safeArea;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            // 스크린 픽셀 기준 Safe Area 바깥 Inset 계산
            float topInsetPx = screenHeight - (safeArea.y + safeArea.height);
            float bottomInsetPx = safeArea.y;
            float leftInsetPx = safeArea.x;
            float rightInsetPx = screenWidth - (safeArea.x + safeArea.width);

            float padTop = 16f;
            float padBottom = 16f;
            float padLeft = 20f;
            float padRight = 20f;

            if (rootContainer.panel != null)
            {
                // UI Toolkit Panel 좌표계로 변환 (pt 단위)
                Vector2 panelTopLeft = RuntimePanelUtils.ScreenToPanel(rootContainer.panel, new Vector2(leftInsetPx, topInsetPx));
                Vector2 panelBottomRight = RuntimePanelUtils.ScreenToPanel(rootContainer.panel, new Vector2(rightInsetPx, bottomInsetPx));

                padTop = Mathf.Max(16f, panelTopLeft.y);
                padBottom = Mathf.Max(16f, panelBottomRight.y);
                padLeft = Mathf.Max(20f, panelTopLeft.x);
                padRight = Mathf.Max(20f, panelBottomRight.x);
            }
            else
            {
                // 패널 미초기화 시 비율(Percent) 또는 픽셀 스케일 환산
                float scale = rootContainer.resolvedStyle.height > 0 ? (rootContainer.resolvedStyle.height / screenHeight) : 1f;
                padTop = Mathf.Max(16f, topInsetPx * scale);
                padBottom = Mathf.Max(16f, bottomInsetPx * scale);
                padLeft = Mathf.Max(20f, leftInsetPx * scale);
                padRight = Mathf.Max(20f, rightInsetPx * scale);
            }

            rootContainer.style.paddingTop = padTop;
            rootContainer.style.paddingBottom = padBottom;
            rootContainer.style.paddingLeft = padLeft;
            rootContainer.style.paddingRight = padRight;
        }

        public void UpdateDifficultyDisplay(AIDifficulty difficulty)
        {
            if (_btnDifficulty != null)
            {
                _btnDifficulty.text = $"{LocalizationManager.Get("Header_Diff_Prefix")}{difficulty.GetDisplayName()}";
            }
            ApplyDifficultyTheme(difficulty);
        }

        public void ApplyDifficultyTheme(AIDifficulty difficulty)
        {
            if (_root == null) return;

            _root.RemoveFromClassList("theme-diff-easy");
            _root.RemoveFromClassList("theme-diff-normal");
            _root.RemoveFromClassList("theme-diff-hard");
            _root.RemoveFromClassList("theme-diff-hell");

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    _root.AddToClassList("theme-diff-easy");
                    break;
                case AIDifficulty.Normal:
                    _root.AddToClassList("theme-diff-normal");
                    break;
                case AIDifficulty.Hard:
                    _root.AddToClassList("theme-diff-hard");
                    break;
                case AIDifficulty.Hell:
                    _root.AddToClassList("theme-diff-hell");
                    break;
            }
        }

        public void SetInteractive(bool active)
        {
            _isInteractive = active;
        }

        private void ToggleDiscardMode()
        {
            _isDiscardMode = !_isDiscardMode;
            UpdateDiscardButtonUI();
            ClearSelection();
            RefreshPlayerPanels();
        }

        public void SetDiscardMode(bool active)
        {
            _isDiscardMode = active;
            UpdateDiscardButtonUI();
            RefreshPlayerPanels();
        }

        private void UpdateDiscardButtonUI()
        {
            if (_btnDiscardMode == null) return;
            if (_isDiscardMode)
            {
                _btnDiscardMode.AddToClassList("discard-btn--active");
                _btnDiscardMode.text = LocalizationManager.Get("Btn_Discard_Active");
            }
            else
            {
                _btnDiscardMode.RemoveFromClassList("discard-btn--active");
                _btnDiscardMode.text = LocalizationManager.Get("Btn_Discard_Default");
            }
        }

        // ──────────────────────────────────────────────
        // 반응형 레이아웃
        // ──────────────────────────────────────────────

        private void OnBoardAreaGeometryChanged(GeometryChangedEvent evt)
        {
            var areaRect = _boardArea.contentRect;
            if (areaRect.width <= 0 || areaRect.height <= 0) return;

            float availableWidth = areaRect.width;
            float availableHeight = areaRect.height;

            float boardWidth, boardHeight;
            if (availableWidth / availableHeight < BoardAspectRatio)
            {
                boardWidth = availableWidth;
                boardHeight = boardWidth / BoardAspectRatio;
            }
            else
            {
                boardHeight = availableHeight;
                boardWidth = boardHeight * BoardAspectRatio;
            }

            _cachedBoardWidth = boardWidth;
            _boardContainer.style.width = boardWidth;
            _boardContainer.style.height = boardHeight;
            _boardContainer.style.flexGrow = 0;
            _boardContainer.style.flexShrink = 0;

            UpdatePieceFontSizes(boardWidth);
        }

        private void UpdatePieceFontSizes(float boardWidth)
        {
            if (boardWidth <= 0) return;
            _cachedBoardWidth = boardWidth;
            int fontSize = Mathf.Max(12, Mathf.RoundToInt(boardWidth * PieceFontRatio));

            foreach (var pieceEl in _pieceElements.Values)
            {
                var pieceLabel = pieceEl.Q<Label>("piece-label");
                if (pieceLabel != null)
                {
                    pieceLabel.style.fontSize = fontSize;
                }
            }
        }

        // ──────────────────────────────────────────────
        // 그리드 구축
        // ──────────────────────────────────────────────

        private void BuildGrid()
        {
            _intersectionsGrid.Clear();

            // Row 9(한) ~ Row 0(초)
            for (int row = BoardPosition.MaxRow - 1; row >= 0; row--)
            {
                var rowContainer = new VisualElement();
                rowContainer.AddToClassList("board-row");

                for (int col = 0; col < BoardPosition.MaxCol; col++)
                {
                    var intersection = CreateIntersection(col, row);
                    rowContainer.Add(intersection);
                    _intersectionElements[col, row] = intersection;
                }

                _intersectionsGrid.Add(rowContainer);
            }
        }

        private VisualElement CreateIntersection(int col, int row)
        {
            var element = new VisualElement();
            element.AddToClassList("intersection");
            element.name = $"cell-{col}-{row}";

            var pos = new BoardPosition(col, row);
            if (pos.IsInsideAnyPalace())
            {
                element.AddToClassList("intersection--palace");
            }

            element.RegisterCallback<ClickEvent>(evt =>
            {
                OnIntersectionClicked(col, row);
            });

            return element;
        }

        // ──────────────────────────────────────────────
        // UI 갱신 (전체)
        // ──────────────────────────────────────────────

        public void SetGameState(Board board, PlayerState choState, PlayerState hanState, PlayerSide currentTurn)
        {
            _board = board;
            _choState = choState;
            _hanState = hanState;
            _currentTurn = currentTurn;
            RefreshAll();
        }

        public void RefreshAll()
        {
            RefreshBoardPieces();
            RefreshPlayerPanels();
            UpdateTurnDisplay(_currentTurn);
            ShowCheckWarning();
        }

        public void RefreshBoardPieces()
        {
            if (_board == null || _pieceLayer == null) return;

            // 1. 현재 보드 위의 살아있는 기물 목록 가져오기
            var alivePieces = _board.GetAllPieces();
            var alivePieceSet = new HashSet<Piece>(alivePieces);

            // 2. 삭제된(죽은) 기물 UI 제거 (애니메이션 포함)
            var piecesToRemove = new List<Piece>();
            bool hasCapture = false;
            foreach (var kvp in _pieceElements)
            {
                var piece = kvp.Key;
                var pieceEl = kvp.Value;

                if (!alivePieceSet.Contains(piece) || !piece.IsAlive)
                {
                    piecesToRemove.Add(piece);
                    pieceEl.AddToClassList("piece-container--exit");
                    
                    // 삭제 스케줄 (애니메이션 끝난 후 DOM에서 제거)
                    var elToRemove = pieceEl;
                    elToRemove.schedule.Execute(() =>
                    {
                        if (elToRemove.parent != null)
                            elToRemove.parent.Remove(elToRemove);
                    }).StartingIn(250);
                    
                    // 타격 이펙트 발생 (공격자 기준 색상)
                    var attacker = _board.GetPieceAt(piece.Position);
                    PlayerSide effectSide = attacker != null ? attacker.Side : piece.Side;
                    PlayCaptureEffect(piece.Position, effectSide);
                    hasCapture = true;
                }
            }

            if (hasCapture)
            {
                PlayBoardShake();
            }

            foreach (var p in piecesToRemove)
            {
                _pieceElements.Remove(p);
                _pieceLastPositions.Remove(p);
            }

            // 3. 살아있는 기물 생성 및 이동 업데이트
            foreach (var piece in alivePieces)
            {
                bool isNew = !_pieceElements.TryGetValue(piece, out var pieceEl);
                if (isNew)
                {
                    // 새로 소환되거나 초기 배치된 기물
                    pieceEl = CreatePieceElement(piece);
                    _pieceLayer.Add(pieceEl);
                    _pieceElements[piece] = pieceEl;
                    _pieceLastPositions[piece] = piece.Position;

                    // 스폰 애니메이션
                    pieceEl.AddToClassList("piece-container--enter");
                    var elToAnimate = pieceEl;
                    elToAnimate.schedule.Execute(() =>
                    {
                        elToAnimate.RemoveFromClassList("piece-container--enter");
                    }).StartingIn(10);

                    // 스폰 이펙트 (마법진)
                    PlaySpawnEffect(piece.Position, piece.Side, 80);
                }
                else
                {
                    // 이동 감지
                    if (_pieceLastPositions.TryGetValue(piece, out var lastPos) && !lastPos.Equals(piece.Position))
                    {
                        var fromPos = lastPos;
                        var toPos = piece.Position;
                        _pieceLastPositions[piece] = toPos;
                        // 보드 상 기물 이동 이펙트 연출 (출발 잔상 + 이동 리프트/슬램 + 착지 충격파)
                        PlayMoveEffect(fromPos, toPos, piece.Side, pieceEl);
                    }
                }

                // 위치 업데이트 (CSS Transition에 의해 부드럽게 이동됨)
                UpdatePieceElementPosition(pieceEl, piece.Position);
            }

            // 직전 수의 이전 위치 하이라이트 갱신 (선택 상태에 따라 겹치면 가리고, 안 겹치면 표시)
            UpdateLastMoveHighlight();

            // 하이라이트 잔상 제거용 교차점 정리
            for (int col = 0; col < BoardPosition.MaxCol; col++)
            {
                for (int row = 0; row < BoardPosition.MaxRow; row++)
                {
                    ClearIntersection(_intersectionElements[col, row]);
                }
            }

            // 보드 크기가 이미 계산되어 있으면 즉시 동기적으로 폰트 크기 보정
            float currentWidth = _cachedBoardWidth > 0 ? _cachedBoardWidth : _boardContainer.resolvedStyle.width;
            if (currentWidth <= 0 && _boardArea != null)
            {
                currentWidth = _boardArea.resolvedStyle.width;
            }

            if (currentWidth > 0)
            {
                UpdatePieceFontSizes(currentWidth);
            }
            else
            {
                _boardContainer.RegisterCallbackOnce<GeometryChangedEvent>(evt =>
                {
                    UpdatePieceFontSizes(evt.newRect.width);
                });
            }
        }

        private VisualElement CreatePieceElement(Piece piece)
        {
            var container = new VisualElement();
            container.AddToClassList("piece-container");
            container.pickingMode = PickingMode.Ignore;

            // 기물 종류에 따른 물리적 크기 조절 (왕은 가장 크고, 신하/쫄은 작게)
            float sizeRatio = 0.86f; // 기본 크기 (Medium: 차, 포, 마, 상)
            switch (piece.Type)
            {
                case PieceType.King: sizeRatio = 0.98f; break; // 왕 (가장 크게)
                case PieceType.Advisor:
                case PieceType.Pawn: sizeRatio = 0.74f; break; // 신하, 쫄 (가장 작게)
            }
            Length sizeLength = Length.Percent(sizeRatio * 100f);

            var shadow = new HexagonElement();
            shadow.AddToClassList("piece-shadow");
            shadow.AddToClassList(piece.Side == PlayerSide.Cho ? "piece-shadow-cho" : "piece-shadow-han");
            shadow.pickingMode = PickingMode.Ignore;
            shadow.style.width = sizeLength;
            shadow.style.height = sizeLength;
            container.Add(shadow);

            var pieceBg = new HexagonElement();
            pieceBg.AddToClassList("piece");
            pieceBg.AddToClassList(piece.Side == PlayerSide.Cho ? "piece-cho" : "piece-han");
            pieceBg.pickingMode = PickingMode.Ignore;
            pieceBg.style.width = sizeLength;
            pieceBg.style.height = sizeLength;

            var pieceLabel = new Label();
            pieceLabel.name = "piece-label";
            pieceLabel.text = piece.GetDisplayName();
            pieceLabel.pickingMode = PickingMode.Ignore;
            // Center the text inside the hexagon
            pieceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            pieceLabel.style.position = Position.Absolute;
            pieceLabel.style.left = 0; pieceLabel.style.right = 0;
            pieceLabel.style.top = 0; pieceLabel.style.bottom = 0;

            if (_cachedBoardWidth > 0)
            {
                int fontSize = Mathf.Max(12, Mathf.RoundToInt(_cachedBoardWidth * PieceFontRatio));
                pieceLabel.style.fontSize = fontSize;
            }

            pieceBg.Add(pieceLabel);
            container.Add(pieceBg);
            return container;
        }

        private void UpdatePieceElementPosition(VisualElement pieceEl, BoardPosition pos)
        {
            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;

            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            pieceEl.style.left = Length.Percent(leftPercent);
            pieceEl.style.top = Length.Percent(topPercent);
        }

        private void PlayMoveEffect(BoardPosition fromPos, BoardPosition toPos, PlayerSide side, VisualElement pieceEl)
        {
            if (_pieceLayer == null) return;

            // 1. 출발 지점 잔상 링 (Departure Echo)
            PlayDepartureEffect(fromPos, side);

            // 2. 이동 중 기물 리프트(들림) 및 착지 슬램(탁!) 애니메이션 (0.2s 이동 동기화)
            if (pieceEl != null)
            {
                pieceEl.AddToClassList("piece-container--moving");
                pieceEl.schedule.Execute(() =>
                {
                    pieceEl.RemoveFromClassList("piece-container--moving");
                    pieceEl.AddToClassList("piece-container--slam");
                    pieceEl.schedule.Execute(() =>
                    {
                        pieceEl.RemoveFromClassList("piece-container--slam");
                    }).StartingIn(120);
                }).StartingIn(200);
            }

            // 3. 도착 지점 착지 충격파 & 코어 섬광 이펙트 (0.2s 착지 시점에 발동)
            PlayLandingEffect(toPos, side, 200);
        }

        private void PlayDepartureEffect(BoardPosition pos, PlayerSide side)
        {
            if (_pieceLayer == null) return;

            var ring = new VisualElement();
            ring.AddToClassList("move-departure-ring");
            ring.AddToClassList($"effect-side-{side.ToString().ToLower()}");
            if (side == PlayerSide.Cho)
            {
                var diff = GameManager.Instance.GetDifficulty();
                ring.AddToClassList($"effect-diff-{diff.ToString().ToLower()}");
            }

            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;
            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            ring.style.left = Length.Percent(leftPercent);
            ring.style.top = Length.Percent(topPercent);

            _pieceLayer.Add(ring);

            ring.schedule.Execute(() =>
            {
                ring.AddToClassList("move-departure-ring--play");
            }).StartingIn(10);

            ring.schedule.Execute(() =>
            {
                if (ring.parent != null)
                    ring.parent.Remove(ring);
            }).StartingIn(320);
        }

        private void PlayLandingEffect(BoardPosition pos, PlayerSide side, int delayMs)
        {
            if (_pieceLayer == null) return;

            var shockwave = new VisualElement();
            shockwave.AddToClassList("move-landing-shockwave");
            shockwave.AddToClassList($"effect-side-{side.ToString().ToLower()}");
            if (side == PlayerSide.Cho)
            {
                var diff = GameManager.Instance.GetDifficulty();
                shockwave.AddToClassList($"effect-diff-{diff.ToString().ToLower()}");
            }

            var core = new VisualElement();
            core.AddToClassList("move-landing-core");
            shockwave.Add(core);

            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;
            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            shockwave.style.left = Length.Percent(leftPercent);
            shockwave.style.top = Length.Percent(topPercent);

            _pieceLayer.Add(shockwave);

            shockwave.schedule.Execute(() =>
            {
                shockwave.AddToClassList("move-landing-shockwave--play");
            }).StartingIn(delayMs);

            shockwave.schedule.Execute(() =>
            {
                if (shockwave.parent != null)
                    shockwave.parent.Remove(shockwave);
            }).StartingIn(delayMs + 350);
        }

        private void PlayImpactEffect(BoardPosition pos, int delayMs)
        {
            PlayLandingEffect(pos, PlayerSide.Cho, delayMs);
        }

        private void PlayCaptureEffect(BoardPosition pos, PlayerSide side)
        {
            if (_pieceLayer == null) return;

            var slash = new VisualElement();
            slash.AddToClassList("capture-slash");
            slash.AddToClassList($"effect-side-{side.ToString().ToLower()}");
            
            if (side == PlayerSide.Cho)
            {
                var diff = GameManager.Instance.GetDifficulty();
                slash.AddToClassList($"effect-diff-{diff.ToString().ToLower()}");
            }

            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;
            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            slash.style.left = Length.Percent(leftPercent);
            slash.style.top = Length.Percent(topPercent);

            _pieceLayer.Add(slash);

            slash.schedule.Execute(() =>
            {
                slash.AddToClassList("capture-slash--play");
            }).StartingIn(10);

            slash.schedule.Execute(() =>
            {
                if (slash.parent != null)
                    slash.parent.Remove(slash);
            }).StartingIn(450);
        }

        private void PlaySpawnEffect(BoardPosition pos, PlayerSide side, int delayMs)
        {
            if (_pieceLayer == null) return;

            var glow = new VisualElement();
            glow.AddToClassList("spawn-glow");
            glow.AddToClassList($"effect-side-{side.ToString().ToLower()}");
            
            if (side == PlayerSide.Cho)
            {
                var diff = GameManager.Instance.GetDifficulty();
                glow.AddToClassList($"effect-diff-{diff.ToString().ToLower()}");
            }
            
            var glowInner = new VisualElement();
            glowInner.AddToClassList("spawn-glow-inner");
            glow.Add(glowInner);

            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;
            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            glow.style.left = Length.Percent(leftPercent);
            glow.style.top = Length.Percent(topPercent);

            _pieceLayer.Add(glow);

            glow.schedule.Execute(() =>
            {
                glow.AddToClassList("spawn-glow--play");
            }).StartingIn(delayMs);

            glow.schedule.Execute(() =>
            {
                if (glow.parent != null)
                    glow.parent.Remove(glow);
            }).StartingIn(delayMs + 650);
        }

        private void PlayBoardShake()
        {
            if (_boardContainer == null) return;
            
            _boardContainer.AddToClassList("board-shake");
            _boardContainer.schedule.Execute(() =>
            {
                _boardContainer.RemoveFromClassList("board-shake");
            }).StartingIn(60);
        }

        private void ClearIntersection(VisualElement element)
        {
            element.RemoveFromClassList("intersection--selected");
            element.RemoveFromClassList("intersection--movable");
            element.RemoveFromClassList("intersection--attackable");
            element.RemoveFromClassList("intersection--spawnable");
            element.RemoveFromClassList("intersection--eliminate-target");
            element.RemoveFromClassList("intersection--check");
            element.RemoveFromClassList("intersection--last-from");
            element.RemoveFromClassList("intersection--last-to");
            element.RemoveFromClassList("intersection--review-attacker");
            element.RemoveFromClassList("intersection--review-controller");
            element.RemoveFromClassList("intersection--review-target-king");
            element.RemoveFromClassList("intersection--review-blocked-pos");
        }

        // ──────────────────────────────────────────────
        // 손패 & 코스트 렌더링
        // ──────────────────────────────────────────────

        public void RefreshPlayerPanels()
        {
            if (_choState != null)
            {
                if (_choCostLabel != null)
                    _choCostLabel.text = LocalizationManager.Get("Cost_Format", _choState.CurrentCost, PlayerState.MaxCost);
                if (_choFieldCostLabel != null)
                    _choFieldCostLabel.text = LocalizationManager.Get("Power_Format", _board?.GetTotalPieceCost(PlayerSide.Cho) ?? 0, PlayerState.MaxFieldCost);
                RenderHand(_choHandContainer, _choState, PlayerSide.Cho, isInteractive: _currentTurn == PlayerSide.Cho);
            }

            if (_hanState != null)
            {
                if (_hanCostLabel != null)
                    _hanCostLabel.text = LocalizationManager.Get("Cost_Format", _hanState.CurrentCost, PlayerState.MaxCost);
                if (_hanFieldCostLabel != null)
                    _hanFieldCostLabel.text = LocalizationManager.Get("Power_Format", _board?.GetTotalPieceCost(PlayerSide.Han) ?? 0, PlayerState.MaxFieldCost);
                RenderHand(_hanHandContainer, _hanState, PlayerSide.Han, isInteractive: false);
            }
        }

        private void RenderHand(VisualElement container, PlayerState state, PlayerSide side, bool isInteractive)
        {
            if (container == null || state == null) return;
            container.Clear();

            for (int i = 0; i < state.Hand.Count; i++)
            {
                int index = i;
                var pieceType = state.Hand[index];
                int cost = pieceType.GetCost();

                var cardWrapper = new VisualElement();
                cardWrapper.AddToClassList("hand-card-wrapper");

                // 카드 뒤에 깔리는 전용 솔리드 섀도우
                var cardShadow = new VisualElement();
                cardShadow.AddToClassList("hand-card-shadow");
                cardShadow.AddToClassList(side == PlayerSide.Cho ? "hand-card-shadow-cho" : "hand-card-shadow-han");
                cardShadow.pickingMode = PickingMode.Ignore;
                cardWrapper.Add(cardShadow);

                var card = new VisualElement();
                card.AddToClassList("hand-card");
                card.pickingMode = PickingMode.Position;

                // 카드 상태 스타일링
                if (_selectedHandCardIndex == index && side == PlayerSide.Cho)
                {
                    card.AddToClassList("hand-card--selected");
                    cardShadow.AddToClassList("hand-card-shadow--selected");
                }

                if (_isDiscardMode && side == PlayerSide.Cho)
                {
                    card.AddToClassList("hand-card--discard-target");
                    cardShadow.AddToClassList("hand-card-shadow--discard");
                }

                bool isUnusable = side == PlayerSide.Cho && !state.CanSummon(_board, pieceType) && !_isDiscardMode;
                if (isUnusable)
                {
                    card.AddToClassList("hand-card--unusable");
                }

                // 코스트 배지
                var costBadge = new VisualElement();
                costBadge.AddToClassList("card-cost-badge");
                costBadge.pickingMode = PickingMode.Ignore;
                var costText = new Label(cost.ToString());
                costText.AddToClassList("card-cost-text");
                costText.pickingMode = PickingMode.Ignore;
                costBadge.Add(costText);
                card.Add(costBadge);

                // 기물 한자 라벨
                var pieceLabel = new Label(pieceType.GetKoreanName(side));
                pieceLabel.AddToClassList("card-piece-label");
                pieceLabel.AddToClassList(side == PlayerSide.Cho ? "card-piece-cho" : "card-piece-han");
                pieceLabel.pickingMode = PickingMode.Ignore;
                card.Add(pieceLabel);

                // 기물 로컬라이즈된 이름 라벨 (한국어: 졸/마/상/포/차, 영어: Pawn/Horse/Elephant/Cannon/Chariot)
                var nameLabel = new Label(LocalizationManager.GetPieceName(pieceType, side));
                nameLabel.AddToClassList("card-name-label");
                nameLabel.pickingMode = PickingMode.Ignore;
                card.Add(nameLabel);

                // 소환 불가 시 카드 표면에만 딤드 오버레이 적용 (뒤 섀도우 박스 투과 방지)
                if (isUnusable)
                {
                    var disabledOverlay = new VisualElement();
                    disabledOverlay.AddToClassList("hand-card-disabled-overlay");
                    disabledOverlay.pickingMode = PickingMode.Ignore;
                    card.Add(disabledOverlay);
                }

                // 클릭 이벤트 바인딩: wrapper에 걸고 evt.StopPropagation()으로 중복 트리거 차단
                if (isInteractive)
                {
                    cardWrapper.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        OnHandCardClicked(index);
                    });
                }

                cardWrapper.Add(card);
                container.Add(cardWrapper);
            }
        }

        private void OnHandCardClicked(int handIndex)
        {
            if (!_isInteractive) return;
            if (_choState == null || _currentTurn != PlayerSide.Cho) return;

            // 1. 패 버리기 모드인 경우
            if (_isDiscardMode)
            {
                OnDiscardRequested?.Invoke(handIndex);
                return;
            }

            // 2. 이미 선택된 카드를 다시 클릭하면 선택 취소
            if (_selectedHandCardIndex == handIndex)
            {
                ClearSelection();
                return;
            }

            // 3. 소환 가능 여부 확인 (코스트 및 필드 전력 20 상한 검증)
            var pieceType = _choState.Hand[handIndex];
            if (!_choState.CanSummon(_board, pieceType))
            {
                if (_choState.HasSummonedThisTurn)
                    ShowStatus(LocalizationManager.Get("Msg_Already_Summoned"));
                else if (_board != null && _board.GetTotalPieceCost(PlayerSide.Cho) + pieceType.GetCost() > PlayerState.MaxFieldCost)
                    ShowStatus(LocalizationManager.Get("Msg_Power_Limit_Exceeded", PlayerState.MaxFieldCost));
                else
                    ShowStatus(LocalizationManager.Get("Msg_Cost_Insufficient"));
                return;
            }

            // 4. 소환 구역 하이라이트
            SelectHandCard(handIndex);
        }

        public void SelectHandCard(int handIndex)
        {
            ClearSelection();

            _selectedHandCardIndex = handIndex;
            var pieceType = _choState.Hand[handIndex];
            _highlightedSpawnPositions = SpawnRuleValidator.GetSpawnablePositions(_board, PlayerSide.Cho, pieceType);

            foreach (var pos in _highlightedSpawnPositions)
            {
                var cell = _intersectionElements[pos.Col, pos.Row];
                cell.AddToClassList("intersection--spawnable");
            }

            // 직전 수의 이전 위치와 겹치는지 체크하여 하이라이트 갱신
            UpdateLastMoveHighlight();

            RefreshPlayerPanels();
            ShowStatus(LocalizationManager.Get("Msg_Select_Spawn_Cell", LocalizationManager.GetPieceName(pieceType, PlayerSide.Cho)));
        }

        // ──────────────────────────────────────────────
        // 기물 선택 / 하이라이트
        // ──────────────────────────────────────────────

        public void SelectPiece(Piece piece, List<BoardPosition> legalMoves)
        {
            ClearSelection();

            _selectedPiece = piece;
            _highlightedMoves = legalMoves ?? new List<BoardPosition>();

            var selectedEl = _intersectionElements[piece.Position.Col, piece.Position.Row];
            selectedEl.AddToClassList("intersection--selected");

            if (_pieceElements.TryGetValue(piece, out var pieceEl))
            {
                pieceEl.AddToClassList("piece-container--selected");
            }

            foreach (var move in _highlightedMoves)
            {
                var moveEl = _intersectionElements[move.Col, move.Row];
                var targetPiece = _board.GetPieceAt(move);

                if (targetPiece != null && targetPiece.Side != piece.Side)
                    moveEl.AddToClassList("intersection--attackable");
                else
                    moveEl.AddToClassList("intersection--movable");
            }

            // 직전 수의 이전 위치와 겹치는지 체크하여 하이라이트 갱신 (겹치면 숨김)
            UpdateLastMoveHighlight();
        }

        /// <summary>
        /// 보드 전체 90개 교차점의 모든 선택 및 이동/소환 하이라이트 클래스를 전수 청소합니다.
        /// 리스트 불일치나 빠른 연속 터치로 인한 잔상을 100% 원천 차단합니다.
        /// </summary>
        public void ClearAllBoardHighlights()
        {
            if (_intersectionElements == null) return;

            for (int col = 0; col < BoardPosition.MaxCol; col++)
            {
                for (int row = 0; row < BoardPosition.MaxRow; row++)
                {
                    var cell = _intersectionElements[col, row];
                    if (cell == null) continue;

                    cell.RemoveFromClassList("intersection--selected");
                    cell.RemoveFromClassList("intersection--movable");
                    cell.RemoveFromClassList("intersection--attackable");
                    cell.RemoveFromClassList("intersection--spawnable");
                    cell.RemoveFromClassList("intersection--eliminate-target");
                }
            }

            foreach (var pieceEl in _pieceElements.Values)
            {
                pieceEl.RemoveFromClassList("piece-container--selected");
            }
        }

        public void ClearSelection(bool clearStatusText = true)
        {
            bool hadSelection = _selectedPiece != null || _selectedHandCardIndex >= 0 || _isEliminateMode;

            // 1. 보드 전체 90개 셀의 하이라이트 클래스를 전수 청소하여 잔상 원천 제거
            ClearAllBoardHighlights();

            // 2. 내부 선택 상태 및 리스트 초기화
            if (_selectedPiece != null && _pieceElements.TryGetValue(_selectedPiece, out var pieceEl))
            {
                pieceEl.RemoveFromClassList("piece-container--selected");
            }
            _selectedPiece = null;
            _highlightedMoves.Clear();
            _selectedHandCardIndex = -1;
            _highlightedSpawnPositions.Clear();

            if (_isEliminateMode)
            {
                _eliminateTargets.Clear();
                _isEliminateMode = false;
                if (_btnAdChance != null)
                {
                    _btnAdChance.RemoveFromClassList("ad-chance-btn--active");
                }
            }

            // 3. 선택 해제 시 직전 수의 이전 위치 하이라이트 복원
            UpdateLastMoveHighlight();

            // 4. 손패 카드 선택 UI 상태 즉시 복원
            RefreshPlayerPanels();

            // 5. 상태 메시지 초기화
            if (clearStatusText && hadSelection)
            {
                ShowStatus("");
            }
        }

        // ──────────────────────────────────────────────
        // 클릭 핸들러 (보드 교차점)
        // ──────────────────────────────────────────────

        private void OnIntersectionClicked(int col, int row)
        {
            if (!_isInteractive || _board == null) return;
            var clickedPos = new BoardPosition(col, row);

            // 0. 적 기물 제거 찬스 모드인 경우
            if (_isEliminateMode)
            {
                if (_eliminateTargets.Contains(clickedPos))
                {
                    var targetPiece = _board.GetPieceAt(clickedPos);
                    if (targetPiece != null)
                    {
                        SetEliminationMode(false, null);
                        OnEliminateTargetSelected?.Invoke(targetPiece);
                        return;
                    }
                }

                // 타겟 외 영역 클릭 시 허무하게 취소되지 않도록 제거 대상 선택 안내 유지
                ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_Select_Target"));
                return;
            }

            // A. 소환 카드 선택 상태에서 소환 가능 구역을 클릭한 경우
            if (_selectedHandCardIndex >= 0 && _highlightedSpawnPositions.Contains(clickedPos))
            {
                OnSpawnRequested?.Invoke(_selectedHandCardIndex, clickedPos);
                return;
            }

            // B. 기물 선택 상태에서 이동 가능 구역을 클릭한 경우
            if (_selectedPiece != null && _highlightedMoves.Contains(clickedPos))
            {
                OnMoveRequested?.Invoke(_selectedPiece, clickedPos);
                return;
            }

            // C. 기물 클릭 (새로운 기물 선택)
            var clickedPiece = _board.GetPieceAt(clickedPos);
            if (clickedPiece != null)
            {
                if (_selectedPiece == clickedPiece)
                {
                    ClearSelection();
                    return;
                }

                OnPieceSelected?.Invoke(clickedPiece);
                return;
            }

            // D. 빈 칸 클릭 -> 선택 해제
            ClearSelection();
            RefreshPlayerPanels();
        }

        // ──────────────────────────────────────────────
        // 정보 표시 유틸리티
        // ──────────────────────────────────────────────

        public void UpdateTurnDisplay(PlayerSide currentTurn)
        {
            _currentTurn = currentTurn;

            if (_root != null)
            {
                if (currentTurn == PlayerSide.Han)
                {
                    _root.RemoveFromClassList("turn-cho");
                    _root.AddToClassList("turn-han");
                }
                else
                {
                    _root.RemoveFromClassList("turn-han");
                    _root.AddToClassList("turn-cho");
                }
            }

            if (_choTurnInfo != null)
            {
                _choTurnInfo.style.display = DisplayStyle.None;
            }
            if (_hanTurnInfo != null)
            {
                _hanTurnInfo.style.display = DisplayStyle.None;
            }
        }

        public void ShowStatus(string message)
        {
            if (_choStatusLabel != null) _choStatusLabel.text = message;
            if (_hanStatusLabel != null) _hanStatusLabel.text = message;
        }

        public void ShowAdLoading(bool show)
        {
            if (_adLoadingOverlay != null)
            {
                _adLoadingOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_btnAdChance != null)
            {
                _btnAdChance.SetEnabled(!show && !_hasUsedAdChance);
            }
        }

        private void ShowCheckWarning()
        {
            if (_board == null) return;

            bool isCheck = false;
            foreach (var side in new[] { PlayerSide.Cho, PlayerSide.Han })
            {
                var king = _board.FindKing(side);
                if (king != null && GameRuleValidator.IsInCheck(_board, side))
                {
                    var kingEl = _intersectionElements[king.Position.Col, king.Position.Row];
                    kingEl.AddToClassList("intersection--check");
                    isCheck = true;
                }
            }

            if (_dangerVignette != null)
            {
                if (isCheck)
                    _dangerVignette.AddToClassList("danger-vignette--active");
                else
                    _dangerVignette.RemoveFromClassList("danger-vignette--active");
            }
        }

        public void SetLastMove(BoardPosition from, BoardPosition to)
        {
            if (_lastMoveFrom.HasValue)
            {
                var prevEl = _intersectionElements[_lastMoveFrom.Value.Col, _lastMoveFrom.Value.Row];
                prevEl?.RemoveFromClassList("intersection--last-from");
            }
            _lastMoveFrom = from;
            _lastMoveTo = to;
            UpdateLastMoveHighlight();
        }

        public void ClearLastMove()
        {
            if (_lastMoveFrom.HasValue)
            {
                var fromEl = _intersectionElements[_lastMoveFrom.Value.Col, _lastMoveFrom.Value.Row];
                fromEl?.RemoveFromClassList("intersection--last-from");
            }
            _lastMoveFrom = null;
            _lastMoveTo = null;
        }

        /// <summary>
        /// 직전 수의 이전 위치(원) 하이라이트를 갱신합니다.
        /// 기물 선택(이동/공격 가능 위치) 또는 소환 위치와 겹치면 숨겼다가,
        /// 겹치지 않거나 선택이 해제되면 다시 표시합니다.
        /// </summary>
        private void UpdateLastMoveHighlight()
        {
            if (!_lastMoveFrom.HasValue) return;

            var fromPos = _lastMoveFrom.Value;
            var fromEl = _intersectionElements[fromPos.Col, fromPos.Row];
            if (fromEl == null) return;

            bool isOverlapped = false;

            // 1. 현재 선택된 기물의 위치와 겹치는지 확인
            if (_selectedPiece != null && _selectedPiece.Position.Equals(fromPos))
            {
                isOverlapped = true;
            }
            // 2. 이동/공격 가능한 위치들과 겹치는지 확인
            else if (_highlightedMoves != null && _highlightedMoves.Contains(fromPos))
            {
                isOverlapped = true;
            }
            // 3. 소환 가능한 위치들과 겹치는지 확인
            else if (_highlightedSpawnPositions != null && _highlightedSpawnPositions.Contains(fromPos))
            {
                isOverlapped = true;
            }
            // 4. 기물 제거 찬스 대상 위치들과 겹치는지 확인
            else if (_isEliminateMode && _eliminateTargets != null && _eliminateTargets.Contains(fromPos))
            {
                isOverlapped = true;
            }

            if (isOverlapped)
            {
                fromEl.RemoveFromClassList("intersection--last-from");
            }
            else
            {
                fromEl.AddToClassList("intersection--last-from");
            }
        }

        /// <summary>
        /// 광고 찬스를 통한 적 기물 제거 모드를 활성화하거나 해제합니다.
        /// </summary>
        public void SetEliminationMode(bool active, List<BoardPosition> targets)
        {
            ClearSelection();

            _isEliminateMode = active;
            _eliminateTargets = targets ?? new List<BoardPosition>();

            if (_isEliminateMode)
            {
                if (_btnAdChance != null)
                {
                    _btnAdChance.AddToClassList("ad-chance-btn--active");
                }

                foreach (var pos in _eliminateTargets)
                {
                    var cell = _intersectionElements[pos.Col, pos.Row];
                    cell?.AddToClassList("intersection--eliminate-target");
                }
            }
            else
            {
                if (_btnAdChance != null)
                {
                    _btnAdChance.RemoveFromClassList("ad-chance-btn--active");
                }

                foreach (var pos in _eliminateTargets)
                {
                    var cell = _intersectionElements[pos.Col, pos.Row];
                    cell?.RemoveFromClassList("intersection--eliminate-target");
                }
                _eliminateTargets.Clear();
            }

            UpdateLastMoveHighlight();
        }

        /// <summary>
        /// 광고 찬스 버튼의 사용 상태 및 스타일을 갱신합니다.
        /// </summary>
        public void SetAdChanceButtonState(bool used, bool enabled)
        {
            _hasUsedAdChance = used;
            if (_btnAdChance == null) return;

            _btnAdChance.SetEnabled(enabled && !used);
            var shadow = _btnAdChance.parent?.Q<VisualElement>(className: "ad-chance-btn-shadow");

            if (used)
            {
                _btnAdChance.text = LocalizationManager.Get("Btn_Ad_Chance_Used");
                _btnAdChance.AddToClassList("ad-chance-btn--disabled");
                _btnAdChance.RemoveFromClassList("ad-chance-btn--active");
                shadow?.AddToClassList("ad-chance-btn-shadow--disabled");
            }
            else
            {
                _btnAdChance.text = LocalizationManager.Get("Btn_Ad_Chance");
                _btnAdChance.RemoveFromClassList("ad-chance-btn--disabled");
                _btnAdChance.RemoveFromClassList("ad-chance-btn--active");
                shadow?.RemoveFromClassList("ad-chance-btn-shadow--disabled");
            }
        }

        // ──────────────────────────────────────────────
        // 장군 / 멍군 다이내믹 알림 배너 (자동 Fade In/Out)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 장군(將軍) 또는 멍군(應將) 배너를 보드 중앙에 팝업하여 페이드인/아웃으로 연출합니다.
        /// 행마한 주체(side)에 따라 플레이어(초)=난이도 Primary, 적 AI(한)=크림슨 레드로 스타일이 분기됩니다.
        /// </summary>
        public void ShowCallout(CalloutType type, PlayerSide side = PlayerSide.Cho)
        {
            if (_calloutBanner == null) return;

            // 기존 예약된 타이머 취소
            _hideCalloutSchedule?.Pause();

            // 스타일 및 텍스트 설정
            _calloutBanner.RemoveFromClassList("callout-banner--check");
            _calloutBanner.RemoveFromClassList("callout-banner--escape");
            _calloutBanner.RemoveFromClassList("callout-banner--side-cho");
            _calloutBanner.RemoveFromClassList("callout-banner--side-han");

            _calloutBanner.AddToClassList($"callout-banner--side-{side.ToString().ToLower()}");

            if (type == CalloutType.Check)
            {
                // 보드 미세 흔들림 (시각적 충격감)
                PlayBoardShake();

                _calloutBanner.AddToClassList("callout-banner--check");
                if (_calloutHanja != null) _calloutHanja.text = "將 軍";
                if (_calloutSubtitle != null)
                {
                    _calloutSubtitle.text = LocalizationManager.Get("Callout_Check");
                }

                // 위협받는 왕(King) 기물 위치에 위기 레이더 펄스 발동
                if (_board != null)
                {
                    foreach (var s in new[] { PlayerSide.Cho, PlayerSide.Han })
                    {
                        var king = _board.FindKing(s);
                        if (king != null && GameRuleValidator.IsInCheck(_board, s))
                        {
                            _pieceElements.TryGetValue(king, out var kingEl);
                            PlayKingDangerPulse(king.Position, kingEl, side == PlayerSide.Han);
                        }
                    }
                }
            }
            else
            {
                _calloutBanner.AddToClassList("callout-banner--escape");
                if (_calloutHanja != null) _calloutHanja.text = "應 將";
                if (_calloutSubtitle != null)
                {
                    _calloutSubtitle.text = LocalizationManager.Get("Callout_Escape");
                }

                // 위험에서 벗어난 왕(King) 기물 위치에 안도 비콘 펄스 발동
                if (_board != null)
                {
                    foreach (var s in new[] { PlayerSide.Cho, PlayerSide.Han })
                    {
                        var king = _board.FindKing(s);
                        if (king != null && !GameRuleValidator.IsInCheck(_board, s))
                        {
                            PlayKingEscapePulse(king.Position, side == PlayerSide.Han);
                        }
                    }
                }
            }

            // 1. 활성화 및 초기 투명 상태 리셋
            _calloutBanner.style.display = DisplayStyle.Flex;
            _calloutBanner.RemoveFromClassList("callout-banner--visible");
            _calloutBanner.AddToClassList("callout-banner--hidden");

            // 2. 즉시 트랜지션을 통해 페이드인 & 스케일업
            _calloutBanner.schedule.Execute(() =>
            {
                _calloutBanner.RemoveFromClassList("callout-banner--hidden");
                _calloutBanner.AddToClassList("callout-banner--visible");
            }).StartingIn(20);

            // 3. 1.05초간 유지 후 페이드아웃 & 스케일다운
            _hideCalloutSchedule = _calloutBanner.schedule.Execute(() =>
            {
                _calloutBanner.RemoveFromClassList("callout-banner--visible");
                _calloutBanner.AddToClassList("callout-banner--hidden");

                // 4. 애니메이션 완료 후 display: none 처리
                _calloutBanner.schedule.Execute(() =>
                {
                    if (_calloutBanner.ClassListContains("callout-banner--hidden"))
                    {
                        _calloutBanner.style.display = DisplayStyle.None;
                    }
                }).StartingIn(320);
            }).StartingIn(1050);
        }

        private void PlayKingDangerPulse(BoardPosition pos, VisualElement kingEl, bool isEnemyAttack)
        {
            if (_pieceLayer == null) return;

            // 1. 왕 기물 자체의 펄스 확대 효과
            if (kingEl != null)
            {
                kingEl.AddToClassList("piece-container--danger");
                kingEl.schedule.Execute(() =>
                {
                    kingEl.RemoveFromClassList("piece-container--danger");
                }).StartingIn(1100);
            }

            // 2. 왕 위치에 퍼져나가는 레이더 비콘 링 2회 생성 (AI 공격은 크림슨 레드, 플레이어 공격은 난이도 primary)
            for (int i = 0; i < 2; i++)
            {
                int delay = i * 220;
                var beacon = new VisualElement();
                beacon.AddToClassList("king-danger-beacon");
                if (isEnemyAttack)
                {
                    beacon.AddToClassList("king-beacon--enemy");
                }

                float colWidth = 100f / BoardPosition.MaxCol;
                float leftPercent = pos.Col * colWidth;
                float rowHeight = 100f / BoardPosition.MaxRow;
                float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

                beacon.style.left = Length.Percent(leftPercent);
                beacon.style.top = Length.Percent(topPercent);

                _pieceLayer.Add(beacon);

                beacon.schedule.Execute(() =>
                {
                    beacon.AddToClassList("king-danger-beacon--play");
                }).StartingIn(delay + 10);

                beacon.schedule.Execute(() =>
                {
                    if (beacon.parent != null)
                        beacon.parent.Remove(beacon);
                }).StartingIn(delay + 600);
            }
        }

        private void PlayKingEscapePulse(BoardPosition pos, bool isEnemy)
        {
            if (_pieceLayer == null) return;

            var beacon = new VisualElement();
            beacon.AddToClassList("king-escape-beacon");
            if (isEnemy)
            {
                beacon.AddToClassList("king-beacon--enemy");
            }

            float colWidth = 100f / BoardPosition.MaxCol;
            float leftPercent = pos.Col * colWidth;
            float rowHeight = 100f / BoardPosition.MaxRow;
            float topPercent = (BoardPosition.MaxRow - 1 - pos.Row) * rowHeight;

            beacon.style.left = Length.Percent(leftPercent);
            beacon.style.top = Length.Percent(topPercent);

            _pieceLayer.Add(beacon);

            beacon.schedule.Execute(() =>
            {
                beacon.AddToClassList("king-escape-beacon--play");
            }).StartingIn(10);

            beacon.schedule.Execute(() =>
            {
                if (beacon.parent != null)
                    beacon.parent.Remove(beacon);
            }).StartingIn(500);
        }
    }
}