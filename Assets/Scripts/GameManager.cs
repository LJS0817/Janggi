using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Janggi.Core;
using Janggi.UI;
using Janggi.AI;

namespace Janggi
{
    /// <summary>
    /// 장기 디펜스 게임의 메인 관리자.
    /// 보드, 자원(코스트), 손패(덱), 소환, 행마, 턴 시퀀스 및 적 AI(PvE)를 총괄합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private PanelRenderer _panelRenderer;

        [Header("AI Settings")]
        [SerializeField] private AIDifficulty _aiDifficulty = AIDifficulty.Normal;

        // 보드 및 플레이어 자원 상태
        private Board _board;
        private PlayerState _choState; // 플레이어 (초)
        private PlayerState _hanState; // 적 AI (한)
        private PlayerSide _currentTurn;
        private bool _gameOver = false;
        private bool _isInGame = false;
        private bool _hasUsedAdChance = false;
        private Coroutine _aiCoroutine;

        // 전투 통계
        private int _turnCount = 1;
        private int _playerCaptures = 0;
        private int _playerSummons = 0;
        private bool _wasCurrentSideInCheckBeforeTurn = false;

        // UI 컨트롤러
        private BoardUIController _uiController;

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void OnEnable()
        {
            InitializeGame();

            if (_panelRenderer != null)
            {
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        private void OnDisable()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            }

            _uiController?.Dispose();
            _uiController = null;

            if (_aiCoroutine != null)
            {
                StopCoroutine(_aiCoroutine);
                _aiCoroutine = null;
            }
        }

        /// <summary>
        /// PanelRenderer의 UI가 로드/리로드될 때 호출되는 콜백.
        /// </summary>
        private void OnUIReload(PanelRenderer renderer, VisualElement root)
        {
            // 이전 컨트롤러 리소스 및 이벤트 구독 해제
            _uiController?.Dispose();

            _uiController = new BoardUIController(root);
            _uiController.SetGameState(_board, _choState, _hanState, _currentTurn);
            _uiController.SelectDifficulty(_aiDifficulty);

            // 이벤트 바인딩
            _uiController.OnPieceSelected = OnPieceSelected;
            _uiController.OnMoveRequested = OnMoveRequested;
            _uiController.OnSpawnRequested = OnSpawnRequested;
            _uiController.OnDiscardRequested = OnDiscardRequested;
            _uiController.OnPassRequested = OnPassRequested;
            _uiController.OnDifficultyToggled = OnDifficultyToggled;
            _uiController.OnRestartRequested = InitializeGame;
            _uiController.OnStartGameRequested = StartGame;
            _uiController.OnReturnToMenuRequested = ReturnToMainMenu;
            _uiController.OnAdChanceRequested = OnAdChanceRequested;
            _uiController.OnEliminateTargetSelected = OnEliminateTargetSelected;
            _uiController.SetAdChanceButtonState(_hasUsedAdChance, true);

            if (_isInGame)
            {
                _uiController.ShowGamePlay();
            }
            else
            {
                _uiController.ShowMainMenu();
            }

            Debug.Log("[Janggi] UI 바인딩 완료 (PanelRenderer).");
        }

        /// <summary>
        /// 메인 메뉴에서 난이도를 선택하고 게임을 시작합니다.
        /// </summary>
        public void StartGame(AIDifficulty difficulty)
        {
            _aiDifficulty = difficulty;
            _isInGame = true;
            _uiController?.ShowGamePlay();
            InitializeGame();
            Debug.Log($"[Janggi] 게임 시작! 난이도: {difficulty.GetDisplayName()}");
        }

        /// <summary>
        /// 인게임에서 메인 메뉴 화면으로 복귀합니다.
        /// </summary>
        public void ReturnToMainMenu()
        {
            if (_aiCoroutine != null)
            {
                StopCoroutine(_aiCoroutine);
                _aiCoroutine = null;
            }

            _isInGame = false;
            _gameOver = true;
            _uiController?.ShowMainMenu();
            Debug.Log("[Janggi] 메인 메뉴로 복귀");
        }

        private void OnDifficultyToggled()
        {
            // Easy -> Normal -> Hard -> Hell -> Easy 순환
            switch (_aiDifficulty)
            {
                case AIDifficulty.Easy:   _aiDifficulty = AIDifficulty.Normal; break;
                case AIDifficulty.Normal: _aiDifficulty = AIDifficulty.Hard;   break;
                case AIDifficulty.Hard:   _aiDifficulty = AIDifficulty.Hell;   break;
                case AIDifficulty.Hell:   _aiDifficulty = AIDifficulty.Easy;   break;
            }

            _uiController?.SelectDifficulty(_aiDifficulty);
            _uiController?.ShowStatus(LocalizationManager.Get("Msg_Diff_Changed", _aiDifficulty.GetDisplayName()));
            Debug.Log($"[Janggi] AI 난이도 변경: {_aiDifficulty}");
        }

        /// <summary>
        /// 게임을 초기화합니다.
        /// </summary>
        public void InitializeGame()
        {
            if (_aiCoroutine != null)
            {
                StopCoroutine(_aiCoroutine);
                _aiCoroutine = null;
            }

            // 1. 보드 및 왕/사 초기 배치 (gemini.md §4)
            _board = new Board();
            _board.SetupInitialPosition();

            // 2. 자원 및 손패 4장 초기화 (양측 모두 자신의 첫 턴 시작 시 0 + 2 = 2 코스트로 시작)
            _choState = new PlayerState(PlayerSide.Cho);
            _hanState = new PlayerState(PlayerSide.Han);
            _choState.StartTurn(); // 선공(초) 첫 턴 코스트 +2 충전 -> 2

            _currentTurn = PlayerSide.Cho; // 초(플레이어) 선공
            _gameOver = false;

            // 통계 리셋
            _turnCount = 1;
            _playerCaptures = 0;
            _playerSummons = 0;
            _wasCurrentSideInCheckBeforeTurn = false;
            _hasUsedAdChance = false;

            // UI 갱신
            if (_uiController != null)
            {
                _uiController.HideGameOverModal();
                _uiController.SetInteractive(true);
                _uiController.SetDiscardMode(false);
                _uiController.ClearSelection();
                _uiController.ClearLastMove();
                _uiController.SetGameState(_board, _choState, _hanState, _currentTurn);
                _uiController.UpdateDifficultyDisplay(_aiDifficulty);
                _uiController.SetAdChanceButtonState(false, true);
                _uiController.ShowStatus(LocalizationManager.Get("Msg_Game_Start"));
            }

            Debug.Log("[Janggi] 게임 초기화 완료. 초(楚) 코스트: " + _choState.CurrentCost + ", 손패 4장 세팅.");
        }

        // ──────────────────────────────────────────────
        // 1. 기물 소환 (선택 행동)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 플레이어가 손패의 카드를 보드에 소환합니다.
        /// </summary>
        private void OnSpawnRequested(int handIndex, BoardPosition spawnPos)
        {
            if (_gameOver || _currentTurn != PlayerSide.Cho) return;

            var currentState = _choState;
            if (handIndex < 0 || handIndex >= currentState.Hand.Count) return;
            var pieceType = currentState.Hand[handIndex];

            // 소환 구역 및 코스트 유효성 검증
            if (!SpawnRuleValidator.CanSpawnAt(_board, _currentTurn, pieceType, spawnPos))
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Cannot_Spawn_Here"));
                return;
            }

            // 필드 전력 상한(20) 검증
            if (_board.GetTotalPieceCost(_currentTurn) + pieceType.GetCost() > PlayerState.MaxFieldCost)
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Power_Limit_Exceeded", PlayerState.MaxFieldCost));
                return;
            }

            // 코스트 지불 및 카드 소모 (새 카드로 즉시 보충)
            if (!currentState.ConsumeCardForSummon(handIndex, _board))
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Cost_Or_Already_Summoned"));
                return;
            }

            _playerSummons++;

            // 보드에 새 기물 배치
            var newPiece = new Piece(pieceType, _currentTurn, spawnPos);
            _board.PlacePiece(newPiece);

            MobileHapticManager.Instance.Trigger(HapticType.Light);

            Debug.Log($"[Janggi] {_currentTurn} {pieceType} 소환됨 @ {spawnPos} (남은 코스트: {currentState.CurrentCost})");

            // UI 갱신 (선택을 비워두어 소환된 기물 또는 기존 기물 중 자유롭게 선택 가능)
            _uiController.ClearSelection();
            _uiController.RefreshBoardPieces();
            _uiController.RefreshPlayerPanels();
            _uiController.ShowStatus(LocalizationManager.Get("Msg_Summon_Success", LocalizationManager.GetPieceName(newPiece.Type, newPiece.Side)));

            // 만약 턴 시작 시 장군 상태였는데 이번 소환으로 장군이 해제되었다면,
            // 기물 소환 자체가 '멍군' 행위가 되어 즉시 턴을 종료합니다.
            if (_wasCurrentSideInCheckBeforeTurn && !GameRuleValidator.IsInCheck(_board, _currentTurn))
            {
                Debug.Log("[Janggi] 소환으로 멍군(장군 방어) 성공! 턴을 자동 종료합니다.");
                ProcessPostMove();
            }
        }

        // ──────────────────────────────────────────────
        // 2. 패 버리기 (선택 행동)
        // ──────────────────────────────────────────────

        private void OnDiscardRequested(int handIndex)
        {
            if (_gameOver || _currentTurn != PlayerSide.Cho) return;

            if (_choState.CurrentCost < PlayerState.DiscardCost)
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Cost_Insufficient_Discard"));
                return;
            }

            var discardedType = _choState.Hand[handIndex];
            _choState.DiscardCard(handIndex);

            MobileHapticManager.Instance.Trigger(HapticType.Light);

            _uiController.SetDiscardMode(false);
            _uiController.ClearSelection();
            _uiController.RefreshPlayerPanels();
            _uiController.ShowStatus(LocalizationManager.Get("Msg_Discard_Success", LocalizationManager.GetPieceName(discardedType, PlayerSide.Cho)));

            Debug.Log($"[Janggi] 패 버리기 완료: {discardedType} -> 남은 코스트: {_choState.CurrentCost}");
        }

        // ──────────────────────────────────────────────
        // 3. 한 수 쉼 (한 턴 쉬기)
        // ──────────────────────────────────────────────

        private void OnPassRequested()
        {
            if (_gameOver || _currentTurn != PlayerSide.Cho) return;

            // 장군(Check) 상태에서는 한 수 쉼 불가
            if (GameRuleValidator.IsInCheck(_board, PlayerSide.Cho))
            {
                MobileHapticManager.Instance.Trigger(HapticType.Warning);
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Cannot_Pass_In_Check"));
                return;
            }

            MobileHapticManager.Instance.Trigger(HapticType.Light);

            _uiController.ClearSelection();
            _uiController.SetDiscardMode(false);
            _uiController.ShowStatus(LocalizationManager.Get("Msg_Player_Passed"));

            Debug.Log("[Janggi] 초(楚) 한 수 쉼 선택 -> 턴 종료");
            ProcessPostMove();
        }

        // ──────────────────────────────────────────────
        // 4. 기물 선택
        // ──────────────────────────────────────────────

        private void OnPieceSelected(Piece piece)
        {
            if (_gameOver || _currentTurn != PlayerSide.Cho) return;

            if (piece.Side != _currentTurn)
            {
                _uiController.ClearSelection();
                return;
            }

            _uiController.SetDiscardMode(false);

            var legalMoves = GameRuleValidator.GetLegalMoves(_board, piece);
            _uiController.SelectPiece(piece, legalMoves);

            Debug.Log($"[Janggi] {piece} 선택됨. 합법수: {legalMoves.Count}개");
        }

        // ──────────────────────────────────────────────
        // 5. 기물 이동/공격 (필수 행동 -> 턴 종료)
        // ──────────────────────────────────────────────

        private void OnMoveRequested(Piece piece, BoardPosition to)
        {
            if (_gameOver || piece.Side != _currentTurn) return;

            if (!GameRuleValidator.IsMoveLegal(_board, piece, to))
            {
                Debug.LogWarning($"[Janggi] 불법 수: {piece} → {to}");
                return;
            }

            ExecuteMove(piece, to);
        }

        private void ExecuteMove(Piece piece, BoardPosition to)
        {
            var from = piece.Position;

            var captured = _board.MovePiece(piece, to);
            if (captured != null)
            {
                if (_currentTurn == PlayerSide.Cho) _playerCaptures++;

                // 왕이 잡히는 것은 장기 규칙상 예외 상황이나, 버그 발생 시 게임이 멈추지 않도록 강제 종료 처리
                if (captured.Type == PieceType.King)
                {
                    _gameOver = true;
                    bool isPlayerWin = _currentTurn == PlayerSide.Cho;
                    string winner = isPlayerWin ? LocalizationManager.Get("Msg_Winner_Player") : LocalizationManager.Get("Msg_Winner_AI");
                    MobileHapticManager.Instance.Trigger(isPlayerWin ? HapticType.Success : HapticType.Error);

                    var reviewData = new BoardReviewData { IsCheckmate = true, IsDraw = false, IsPlayerWin = isPlayerWin };
                    reviewData.RebuildExplanation();
                    
                    _uiController.ShowStatus(LocalizationManager.Get("Msg_Checkmate_Winner", winner));
                    _uiController.ShowGameOverModal(isWin: isPlayerWin, isDraw: false, _aiDifficulty, _turnCount, _playerCaptures, _playerSummons, reviewData);
                    Debug.Log($"[Janggi] 왕이 직접 잡혔습니다! {winner} 승리!");
                    return;
                }

                // 상대 기물 처치 시 코스트 +1 획득 (최대 10 코스트)
                var currentPlayerState = GetCurrentPlayerState();
                currentPlayerState?.AddCost(PlayerState.CaptureCostGain);

                MobileHapticManager.Instance.Trigger(HapticType.Medium);
                Debug.Log($"[Janggi] {captured} 잡힘! ({_currentTurn} 코스트 +{PlayerState.CaptureCostGain} 획득 -> {currentPlayerState?.CurrentCost}, 총 처치: {_playerCaptures})");
            }
            else
            {
                MobileHapticManager.Instance.Trigger(HapticType.Light);
            }

            Debug.Log($"[Janggi] {_currentTurn} {piece.Type}: {from} → {to}");

            _uiController.ClearSelection();
            _uiController.SetLastMove(from, to);
            _uiController.RefreshBoardPieces();

            ProcessPostMove();
        }

        /// <summary>
        /// 필수 행동(이동) 완료 후 승패 판정 및 턴 교대를 처리합니다.
        /// </summary>
        private void ProcessPostMove()
        {
            var nextTurn = _currentTurn.Opposite();

            // 2. 외통수(체크메이트) 체크
            if (GameRuleValidator.IsCheckmate(_board, nextTurn))
            {
                _gameOver = true;
                bool isPlayerWin = _currentTurn == PlayerSide.Cho;
                string winner = isPlayerWin ? LocalizationManager.Get("Msg_Winner_Player") : LocalizationManager.Get("Msg_Winner_AI");
                
                if (isPlayerWin)
                {
                    MobileHapticManager.Instance.Trigger(HapticType.Success);
                }
                else
                {
                    MobileHapticManager.Instance.Trigger(HapticType.Error);
                }

                var reviewData = GameRuleValidator.AnalyzeGameOver(_board, loserSide: nextTurn, isDraw: false, isPlayerWin: isPlayerWin);
                _uiController.ShowStatus(LocalizationManager.Get("Msg_Checkmate_Winner", winner));
                _uiController.ShowGameOverModal(isWin: isPlayerWin, isDraw: false, _aiDifficulty, _turnCount, _playerCaptures, _playerSummons, reviewData);
                Debug.Log($"[Janggi] 외통수! {winner} 승리!");
                return;
            }

            // 3. 스테일메이트 체크
            if (GameRuleValidator.IsStalemate(_board, nextTurn))
            {
                _gameOver = true;
                MobileHapticManager.Instance.Trigger(HapticType.Medium);
                var reviewData = GameRuleValidator.AnalyzeGameOver(_board, loserSide: nextTurn, isDraw: true, isPlayerWin: false);
                _uiController.ShowStatus(LocalizationManager.Get("Msg_Stalemate"));
                _uiController.ShowGameOverModal(isWin: false, isDraw: true, _aiDifficulty, _turnCount, _playerCaptures, _playerSummons, reviewData);
                Debug.Log("[Janggi] 교착 상태(Stalemate) — 무승부!");
                return;
            }

            // 3-1. 장군(Check) / 멍군(Escape) 판단 및 배너 연출
            bool isOpponentInCheck = GameRuleValidator.IsInCheck(_board, nextTurn);

            if (isOpponentInCheck)
            {
                // 상대 왕을 장군(Check)으로 위협! (중간 강도 햅틱)
                MobileHapticManager.Instance.Trigger(HapticType.Medium);
                _uiController?.ShowCallout(CalloutType.Check, _currentTurn);
            }
            else if (_wasCurrentSideInCheckBeforeTurn)
            {
                // 장군 상태에서 성공적으로 벗어난 경우에만 멍군 연출
                if (!GameRuleValidator.IsInCheck(_board, _currentTurn))
                {
                    // 멍군! 중간 강도 햅틱
                    MobileHapticManager.Instance.Trigger(HapticType.Medium);
                    _uiController?.ShowCallout(CalloutType.Escape, _currentTurn);
                }
            }

            // 4. 턴 교대 및 새 턴 자원 충전 (+2, gemini.md §3)
            if (nextTurn == PlayerSide.Cho)
            {
                _turnCount++;
            }

            _currentTurn = nextTurn;
            var nextState = GetCurrentPlayerState();
            nextState.StartTurn();

            // 다음 턴 플레이어의 시작 시점 장군 여부 기록
            _wasCurrentSideInCheckBeforeTurn = GameRuleValidator.IsInCheck(_board, _currentTurn);

            _uiController.SetDiscardMode(false);
            _uiController.ClearSelection();
            _uiController.SetGameState(_board, _choState, _hanState, _currentTurn);

            // 5. 장군 상태 알림
            if (GameRuleValidator.IsInCheck(_board, _currentTurn))
            {
                string checkedSide = _currentTurn == PlayerSide.Cho
                    ? LocalizationManager.Get("Msg_Side_Cho_Short")
                    : LocalizationManager.Get("Msg_Side_Han_Short");
                _uiController.ShowStatus(LocalizationManager.Get("Msg_Check_Warning", checkedSide));
            }
            else
            {
                _uiController.ShowStatus("");
            }

            // 6. 적 AI 턴인 경우 자동 실행
            if (_currentTurn == PlayerSide.Han && !_gameOver)
            {
                _uiController.SetInteractive(false);
                _aiCoroutine = StartCoroutine(ExecuteAITurnCoroutine());
            }
            else
            {
                _uiController.SetInteractive(true);
            }

            Debug.Log($"[Janggi] {_currentTurn}의 턴 시작 (코스트: {nextState.CurrentCost}/10)");
        }

        // ──────────────────────────────────────────────
        // 5. PvE 적 AI 턴 실행 코루틴
        // ──────────────────────────────────────────────

        private IEnumerator ExecuteAITurnCoroutine()
        {
            _uiController.ShowStatus(LocalizationManager.Get("Msg_AI_Thinking"));
            yield return new WaitForSeconds(0.6f);

            if (_gameOver) yield break;

            // 1단계: AI 소환 판단 (gemini.md §5 난이도별 소환)
            var (shouldSpawn, handIndex, spawnPos) = JanggiAIController.DecideSpawn(_board, _hanState, _choState, _aiDifficulty);

            if (shouldSpawn && handIndex >= 0 && handIndex < _hanState.Hand.Count)
            {
                var pieceType = _hanState.Hand[handIndex];
                if (SpawnRuleValidator.CanSpawnAt(_board, PlayerSide.Han, pieceType, spawnPos))
                {
                    // 코스트 차감 및 소환 1회 완료 검증
                    if (_hanState.ConsumeCardForSummon(handIndex, _board))
                    {
                        var aiNewPiece = new Piece(pieceType, PlayerSide.Han, spawnPos);
                        _board.PlacePiece(aiNewPiece);

                        _uiController.RefreshBoardPieces();
                        _uiController.RefreshPlayerPanels();
                        _uiController.ShowStatus(LocalizationManager.Get("Msg_AI_Summoned", LocalizationManager.GetPieceName(aiNewPiece.Type, aiNewPiece.Side), _hanState.CurrentCost));

                        Debug.Log($"[Janggi] AI {pieceType} 소환 완료 (소모: {pieceType.GetCost()}, 남은 코스트: {_hanState.CurrentCost})");
                        yield return new WaitForSeconds(0.5f);
                        
                        // 만약 AI가 장군 상태였는데 소환으로 방어에 성공했다면 즉시 턴 종료
                        if (_wasCurrentSideInCheckBeforeTurn && !GameRuleValidator.IsInCheck(_board, PlayerSide.Han))
                        {
                            Debug.Log("[Janggi] AI 소환으로 멍군(장군 방어) 성공! 턴을 자동 종료합니다.");
                            ProcessPostMove();
                            _aiCoroutine = null;
                            yield break;
                        }
                    }
                }
            }

            if (_gameOver) yield break;

            // 2단계: AI 행마 판단 (gemini.md §5 난이도별 이동)
            var (bestPiece, bestTargetPos) = JanggiAIController.DecideMove(_board, _hanState, _choState, _aiDifficulty);

            if (bestPiece != null)
            {
                _uiController.ShowStatus(LocalizationManager.Get("Msg_AI_Moved", LocalizationManager.GetPieceName(bestPiece.Type, bestPiece.Side)));
                yield return new WaitForSeconds(0.3f);

                ExecuteMove(bestPiece, bestTargetPos);
            }
            else
            {
                // 합법수가 없는 경우(외통수/스테일메이트)
                if (GameRuleValidator.IsInCheck(_board, PlayerSide.Han))
                {
                    _gameOver = true;
                    var reviewData = GameRuleValidator.AnalyzeGameOver(_board, loserSide: PlayerSide.Han, isDraw: false, isPlayerWin: true);
                    _uiController.ShowStatus(LocalizationManager.Get("Msg_Checkmate_Winner", LocalizationManager.Get("Msg_Winner_Player")));
                    _uiController.ShowGameOverModal(isWin: true, isDraw: false, _aiDifficulty, _turnCount, _playerCaptures, _playerSummons, reviewData);
                    Debug.Log("[Janggi] AI 스스로 외통수(합법수 없음). 플레이어 승리!");
                }
                else
                {
                    _gameOver = true;
                    var reviewData = GameRuleValidator.AnalyzeGameOver(_board, loserSide: PlayerSide.Han, isDraw: true, isPlayerWin: false);
                    _uiController.ShowStatus(LocalizationManager.Get("Msg_Stalemate"));
                    _uiController.ShowGameOverModal(isWin: false, isDraw: true, _aiDifficulty, _turnCount, _playerCaptures, _playerSummons, reviewData);
                    Debug.Log("[Janggi] AI 스테일메이트(합법수 없음). 무승부!");
                }
            }

            _aiCoroutine = null;
        }

        private PlayerState GetCurrentPlayerState()
        {
            return _currentTurn == PlayerSide.Cho ? _choState : _hanState;
        }

        // ──────────────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────────────

        public Board GetBoard() => _board;
        public PlayerSide GetCurrentTurn() => _currentTurn;
        public PlayerState GetChoState() => _choState;
        public PlayerState GetHanState() => _hanState;
        public PlayerState GetPlayerState(PlayerSide side) => side == PlayerSide.Cho ? _choState : _hanState;
        public AIDifficulty GetDifficulty() => _aiDifficulty;
        public bool IsGameOver() => _gameOver;

        // ──────────────────────────────────────────────
        // 6. 구글 광고 찬스 (적 기물 제거)
        // ──────────────────────────────────────────────

        private void OnAdChanceRequested()
        {
            if (_gameOver) return;

            if (_currentTurn != PlayerSide.Cho)
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_Not_Player_Turn"));
                return;
            }

            if (_hasUsedAdChance)
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_Already_Used"));
                return;
            }

            var validTargets = _board.GetPiecesBySide(PlayerSide.Han)
                .FindAll(p => p.IsAlive && p.Type != PieceType.King && p.Type != PieceType.Advisor);

            if (validTargets == null || validTargets.Count == 0)
            {
                _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_No_Target"));
                return;
            }

            _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Loading"));
            _uiController?.ShowAdLoading(true);

            GoogleAdManager.Instance.ShowRewardedAd(isSuccess =>
            {
                _uiController?.ShowAdLoading(false);
                
                if (isSuccess)
                {
                    _hasUsedAdChance = true;
                    _uiController?.SetAdChanceButtonState(true, false);

                    // 다시 유효 대상 검색 (광고 시청 사이 보드 상태 대비)
                    var currentTargets = _board.GetPiecesBySide(PlayerSide.Han)
                        .FindAll(p => p.IsAlive && p.Type != PieceType.King && p.Type != PieceType.Advisor);

                    if (currentTargets.Count > 0)
                    {
                        var targetPositions = currentTargets.ConvertAll(p => p.Position);
                        _uiController?.SetEliminationMode(true, targetPositions);
                        _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_Select_Target"));
                        MobileHapticManager.Instance.Trigger(HapticType.Success);
                    }
                    else
                    {
                        _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_No_Target"));
                    }
                }
                else
                {
                    _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Cancelled"));
                }
            });
        }

        private void OnEliminateTargetSelected(Piece targetPiece)
        {
            if (targetPiece == null || !targetPiece.IsAlive) return;

            var removedType = targetPiece.Type;
            _board.RemovePiece(targetPiece);
            _playerCaptures++;

            // 광고 찬스로 적 기물 제거 시에도 코스트 +1 획득
            _choState.AddCost(PlayerState.CaptureCostGain);

            MobileHapticManager.Instance.Trigger(HapticType.Heavy);
            _uiController?.RefreshAll();

            string pieceName = LocalizationManager.GetPieceName(removedType, PlayerSide.Han);
            _uiController?.ShowStatus(LocalizationManager.Get("Msg_Ad_Chance_Eliminated", pieceName));
            Debug.Log($"[Janggi] 광고 찬스: 적 기물 [{pieceName}] 제거됨! -> 턴 종료 및 턴 교대");

            // 기물 제거 완료 후 승패 판정 및 턴 교대
            ProcessPostMove();
        }
    }
}
