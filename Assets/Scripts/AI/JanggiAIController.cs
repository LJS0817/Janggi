using System;
using System.Collections.Generic;
using Janggi.Core;
using Janggi.Core.Movement;

namespace Janggi.AI
{
    /// <summary>
    /// gemini.md §5를 완벽히 준수하는 장기 PvE AI 컨트롤러.
    /// 4단계 난이도(하/중/상/극악)별 소환 및 행마 알고리즘을 제공합니다.
    /// </summary>
    public static class JanggiAIController
    {
        private static readonly Random _random = new Random();

        // ──────────────────────────────────────────────
        // 1. 소환 판단 (DecideSpawn)
        // ──────────────────────────────────────────────

        /// <summary>
        /// AI의 손패와 현재 보드 상태를 분석하여 기물 소환 여부, 손패 인덱스, 소환 좌표를 결정합니다.
        /// </summary>
        public static (bool shouldSpawn, int handIndex, BoardPosition spawnPos) DecideSpawn(
            Board board, PlayerState aiState, PlayerState playerState, AIDifficulty difficulty)
        {
            if (aiState.HasSummonedThisTurn || aiState.Hand.Count == 0)
                return (false, -1, default);

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    return DecideSpawnEasy(board, aiState);

                case AIDifficulty.Normal:
                    return DecideSpawnNormal(board, aiState);

                case AIDifficulty.Hard:
                    return DecideSpawnHard(board, aiState, playerState);

                case AIDifficulty.Hell:
                    return DecideSpawnHell(board, aiState, playerState);

                default:
                    return DecideSpawnNormal(board, aiState);
            }
        }

        /// <summary>[하] 코스트가 모이는 대로 무작위 소환</summary>
        private static (bool, int, BoardPosition) DecideSpawnEasy(Board board, PlayerState aiState)
        {
            var affordableIndices = new List<int>();
            for (int i = 0; i < aiState.Hand.Count; i++)
            {
                if (aiState.CanSummon(board, aiState.Hand[i]))
                    affordableIndices.Add(i);
            }

            if (affordableIndices.Count == 0) return (false, -1, default);

            // 무작위 카드 선택
            int chosenIndex = affordableIndices[_random.Next(affordableIndices.Count)];
            var pieceType = aiState.Hand[chosenIndex];

            var spawnPositions = SpawnRuleValidator.GetSpawnablePositions(board, PlayerSide.Han, pieceType);
            if (spawnPositions.Count == 0) return (false, -1, default);

            var chosenPos = spawnPositions[_random.Next(spawnPositions.Count)];
            return (true, chosenIndex, chosenPos);
        }

        /// <summary>[중] 기물 가치를 계산하여 자원 비축, 위험 시 방어 소환</summary>
        private static (bool, int, BoardPosition) DecideSpawnNormal(Board board, PlayerState aiState)
        {
            bool isHanInCheck = GameRuleValidator.IsInCheck(board, PlayerSide.Han);

            // 위험 상황이 아니고 코스트가 4 미만이면 자원을 모으기 위해 50% 확률로 소환 대기
            if (!isHanInCheck && aiState.CurrentCost < 4 && _random.NextDouble() < 0.5)
            {
                return (false, -1, default);
            }

            int bestCardIndex = -1;
            BoardPosition bestPos = default;
            int bestScore = int.MinValue;

            for (int i = 0; i < aiState.Hand.Count; i++)
            {
                var pieceType = aiState.Hand[i];
                if (!aiState.CanSummon(board, pieceType)) continue;

                var spawnPositions = SpawnRuleValidator.GetSpawnablePositions(board, PlayerSide.Han, pieceType);
                foreach (var pos in spawnPositions)
                {
                    // 시뮬레이션
                    var simBoard = board.Clone();
                    simBoard.PlacePiece(new Piece(pieceType, PlayerSide.Han, pos));

                    int score = BoardEvaluator.Evaluate(simBoard, PlayerSide.Han);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCardIndex = i;
                        bestPos = pos;
                    }
                }
            }

            if (bestCardIndex >= 0)
                return (true, bestCardIndex, bestPos);

            return (false, -1, default);
        }

        /// <summary>[상] 소환 스나이핑(소환 즉시 공격) 우선 탐색 + 상대 소환지 점거 시도</summary>
        private static (bool, int, BoardPosition) DecideSpawnHard(Board board, PlayerState aiState, PlayerState playerState)
        {
            // 1순위: 소환 직후 상대 기물을 잡을 수 있는 '소환 스나이핑' 기물 우선 탐색
            for (int i = 0; i < aiState.Hand.Count; i++)
            {
                var pieceType = aiState.Hand[i];
                if (!aiState.CanSummon(board, pieceType)) continue;

                var spawnPositions = SpawnRuleValidator.GetSpawnablePositions(board, PlayerSide.Han, pieceType);
                foreach (var pos in spawnPositions)
                {
                    var simBoard = board.Clone();
                    var newPiece = new Piece(pieceType, PlayerSide.Han, pos);
                    simBoard.PlacePiece(newPiece);

                    // 소환된 기물이 이동하여 적을 공격할 수 있는지 확인
                    var legalMoves = GameRuleValidator.GetLegalMoves(simBoard, newPiece);
                    foreach (var move in legalMoves)
                    {
                        var target = simBoard.GetPieceAt(move);
                        if (target != null && target.Side == PlayerSide.Cho)
                        {
                            // 소환 스나이핑 성공!
                            return (true, i, pos);
                        }
                    }
                }
            }

            // 2순위: 일반적인 최적 소환 (Normal 로직 기반)
            return DecideSpawnNormal(board, aiState);
        }

        /// <summary>[극악] 외통수 방어, 외통수 턴 올인 및 플레이어 공개 패를 분석한 사전 수비 소환</summary>
        private static (bool, int, BoardPosition) DecideSpawnHell(Board board, PlayerState aiState, PlayerState playerState)
        {
            // 1순위: 내 왕이 위험할 때, 방어벽을 세울 수 있는 소환 탐색 (Defensive Spawning)
            if (GameRuleValidator.IsInCheck(board, PlayerSide.Han))
            {
                for (int i = 0; i < aiState.Hand.Count; i++)
                {
                    var pieceType = aiState.Hand[i];
                    if (!aiState.CanSummon(board, pieceType)) continue;

                    var spawnPositions = SpawnRuleValidator.GetSpawnablePositions(board, PlayerSide.Han, pieceType);
                    foreach (var pos in spawnPositions)
                    {
                        var simBoard = board.Clone();
                        simBoard.PlacePiece(new Piece(pieceType, PlayerSide.Han, pos));

                        // 소환 후 장군을 피했는지 확인
                        if (!GameRuleValidator.IsInCheck(simBoard, PlayerSide.Han))
                        {
                            return (true, i, pos);
                        }
                    }
                }
            }

            // 2순위: 즉시 외통수를 낼 수 있는 소환 조합 탐색
            for (int i = 0; i < aiState.Hand.Count; i++)
            {
                var pieceType = aiState.Hand[i];
                if (!aiState.CanSummon(board, pieceType)) continue;

                var spawnPositions = SpawnRuleValidator.GetSpawnablePositions(board, PlayerSide.Han, pieceType);
                foreach (var pos in spawnPositions)
                {
                    var simBoard = board.Clone();
                    var newPiece = new Piece(pieceType, PlayerSide.Han, pos);
                    simBoard.PlacePiece(newPiece);

                    var legalMoves = GameRuleValidator.GetLegalMoves(simBoard, newPiece);
                    foreach (var move in legalMoves)
                    {
                        var afterMoveBoard = simBoard.Clone();
                        var simMovedPiece = afterMoveBoard.GetPieceAt(newPiece.Position);
                        afterMoveBoard.MovePiece(simMovedPiece, move);

                        if (GameRuleValidator.IsCheckmate(afterMoveBoard, PlayerSide.Cho))
                        {
                            // 즉시 외통수 소환!
                            return (true, i, pos);
                        }
                    }
                }
            }

            // 2순위: 소환 스나이핑 및 수비적 최적화
            return DecideSpawnHard(board, aiState, playerState);
        }

        // ──────────────────────────────────────────────
        // 2. 이동 판단 (DecideMove)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 보드 상태에서 AI 난이도에 맞는 최적의 이동 합법수를 결정합니다.
        /// </summary>
        public static (Piece piece, BoardPosition targetPos) DecideMove(
            Board board, PlayerState aiState, PlayerState playerState, AIDifficulty difficulty)
        {
            var allLegalMoves = GameRuleValidator.GetAllLegalMovesForSide(board, PlayerSide.Han);
            if (allLegalMoves.Count == 0)
                return (null, default);

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    return DecideMoveEasy(board, allLegalMoves);

                case AIDifficulty.Normal:
                    return DecideMoveMinimax(board, allLegalMoves, depth: 2);

                case AIDifficulty.Hard:
                    return DecideMoveMinimax(board, allLegalMoves, depth: 3);

                case AIDifficulty.Hell:
                    return DecideMoveMinimax(board, allLegalMoves, depth: 4);

                default:
                    return DecideMoveMinimax(board, allLegalMoves, depth: 2);
            }
        }

        /// <summary>[하] 30% 무작위 수, 70% 1수 앞 탐색</summary>
        private static (Piece, BoardPosition) DecideMoveEasy(Board board, List<(Piece piece, BoardPosition to)> moves)
        {
            if (_random.NextDouble() < 0.3)
            {
                return moves[_random.Next(moves.Count)];
            }
            
            return DecideMoveMinimax(board, moves, depth: 1);
        }

        /// <summary>
        /// 탐색 최적화를 위한 MVV-LVA (가장 가치 있는 적을 가장 가치가 낮은 기물로 잡는 수 우선) 정렬
        /// </summary>
        private static void SortMovesByMVVLVA(Board board, List<(Piece piece, BoardPosition to)> moves)
        {
            moves.Sort((a, b) =>
            {
                var targetA = board.GetPieceAt(a.to);
                var targetB = board.GetPieceAt(b.to);
                
                int scoreA = 0;
                int scoreB = 0;

                if (targetA != null)
                {
                    scoreA = BoardEvaluator.GetPieceValue(targetA.Type) * 10 - BoardEvaluator.GetPieceValue(a.piece.Type);
                }
                
                if (targetB != null)
                {
                    scoreB = BoardEvaluator.GetPieceValue(targetB.Type) * 10 - BoardEvaluator.GetPieceValue(b.piece.Type);
                }
                
                // 가치가 같으면 앞으로 전진하는 수를 조금 더 선호 (단순 평가)
                if (scoreA == scoreB)
                {
                    int forwardA = (a.piece.Side == PlayerSide.Han) ? (a.piece.Position.Row - a.to.Row) : (a.to.Row - a.piece.Position.Row);
                    int forwardB = (b.piece.Side == PlayerSide.Han) ? (b.piece.Position.Row - b.to.Row) : (b.to.Row - b.piece.Position.Row);
                    scoreA += forwardA;
                    scoreB += forwardB;
                }

                return scoreB.CompareTo(scoreA); // 내림차순 정렬
            });
        }

        /// <summary>[중/상/극악] Minimax with Alpha-Beta Pruning 탐색</summary>
        private static (Piece, BoardPosition) DecideMoveMinimax(
            Board board, List<(Piece piece, BoardPosition to)> moves, int depth)
        {
            (Piece bestPiece, BoardPosition bestTo) = moves[0];
            int bestValue = int.MinValue;
            int alpha = int.MinValue;
            int beta = int.MaxValue;

            SortMovesByMVVLVA(board, moves);

            foreach (var m in moves)
            {
                var simBoard = board.Clone();
                var simPiece = simBoard.GetPieceAt(m.piece.Position);
                simBoard.MovePiece(simPiece, m.to);

                int val = Minimax(simBoard, depth - 1, alpha, beta, isMaximizing: false);

                if (val > bestValue)
                {
                    bestValue = val;
                    bestPiece = m.piece;
                    bestTo = m.to;
                }

                alpha = Math.Max(alpha, bestValue);
                if (beta <= alpha)
                    break;
            }

            return (bestPiece, bestTo);
        }

        private static int Minimax(Board board, int depth, int alpha, int beta, bool isMaximizing)
        {
            if (depth == 0 || GameRuleValidator.IsCheckmate(board, PlayerSide.Cho) || GameRuleValidator.IsCheckmate(board, PlayerSide.Han))
            {
                return BoardEvaluator.Evaluate(board, PlayerSide.Han);
            }

            var currentSide = isMaximizing ? PlayerSide.Han : PlayerSide.Cho;
            var legalMoves = GameRuleValidator.GetAllLegalMovesForSide(board, currentSide);

            if (legalMoves.Count == 0)
            {
                return BoardEvaluator.Evaluate(board, PlayerSide.Han);
            }

            SortMovesByMVVLVA(board, legalMoves);

            if (isMaximizing)
            {
                int maxEval = int.MinValue;
                foreach (var m in legalMoves)
                {
                    var simBoard = board.Clone();
                    var simPiece = simBoard.GetPieceAt(m.piece.Position);
                    simBoard.MovePiece(simPiece, m.to);

                    int eval = Minimax(simBoard, depth - 1, alpha, beta, false);
                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break;
                }
                return maxEval;
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (var m in legalMoves)
                {
                    var simBoard = board.Clone();
                    var simPiece = simBoard.GetPieceAt(m.piece.Position);
                    simBoard.MovePiece(simPiece, m.to);

                    int eval = Minimax(simBoard, depth - 1, alpha, beta, true);
                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break;
                }
                return minEval;
            }
        }
    }
}
