using System;
using Janggi.Core;

namespace Janggi.AI
{
    /// <summary>
    /// 장기 보드 상태를 정량적으로 평가하는 엔진.
    /// 기물 가치(차>포>마/상>졸), 위치 가중치, 장군/외통수, 스폰 블로킹을 평가합니다.
    /// </summary>
    public static class BoardEvaluator
    {
        public const int CheckmateScore = 99999;
        public const int CheckBonus = 50;

        /// <summary>
        /// 기물의 기본 점수 (장기 표준 가치 기반)
        /// 차(130) > 포(70) > 마(50) > 상(30) = 사(30) > 졸(20)
        /// </summary>
        public static int GetPieceValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.King:     return 10000;
                case PieceType.Chariot:  return 130;
                case PieceType.Cannon:   return 70;
                case PieceType.Horse:    return 50;
                case PieceType.Elephant: return 30;
                case PieceType.Advisor:  return 30;
                case PieceType.Pawn:     return 20;
                default:                 return 0;
            }
        }

        /// <summary>
        /// 특정 진영(forSide)의 관점에서 현재 보드의 점수를 계산합니다. (점수가 높을수록 유리)
        /// </summary>
        public static int Evaluate(Board board, PlayerSide forSide)
        {
            var oppositeSide = forSide.Opposite();

            // 1. 외통수(체크메이트) 판정
            if (GameRuleValidator.IsCheckmate(board, oppositeSide))
                return CheckmateScore; // 내가 승리

            if (GameRuleValidator.IsCheckmate(board, forSide))
                return -CheckmateScore; // 내가 패배

            int score = 0;

            // 3. 기물 점수 및 위치 가중치 합산
            var allPieces = board.GetAllPieces();
            foreach (var piece in allPieces)
            {
                int pieceScore = GetPieceValue(piece.Type) + GetPositionBonus(board, piece);

                if (piece.Side == forSide)
                    score += pieceScore;
                else
                    score -= pieceScore;
            }

            // 4. 장군 보너스
            if (GameRuleValidator.IsInCheck(board, oppositeSide))
                score += CheckBonus; // 상대에게 장군을 침

            if (GameRuleValidator.IsInCheck(board, forSide))
                score -= CheckBonus; // 내가 장군을 당함

            return score;
        }

        /// <summary>
        /// 기물의 위치 및 방어 상태에 따른 전술적 보너스 점수를 계산합니다.
        /// </summary>
        private static int GetPositionBonus(Board board, Piece piece)
        {
            int bonus = 0;
            var pos = piece.Position;
            bool isHan = piece.Side == PlayerSide.Han;

            // 1. 궁성 내 수비력 평가
            if (piece.Type == PieceType.King)
            {
                // 왕은 궁성 중앙(4, 1 or 4, 8)에 있을 때 가장 안전함
                int idealRow = isHan ? 8 : 1;
                if (pos.Col == 4 && pos.Row == idealRow) bonus += 5;
                
                // 사(Advisor)가 왕 주변에 있는지 확인 (수비력 가산점)
                int advisorDefenders = 0;
                var myPieces = board.GetPiecesBySide(piece.Side);
                foreach (var p in myPieces)
                {
                    if (p.Type == PieceType.Advisor && Math.Abs(p.Position.Col - pos.Col) <= 1 && Math.Abs(p.Position.Row - pos.Row) <= 1)
                    {
                        advisorDefenders++;
                    }
                }
                bonus += advisorDefenders * 5; // 수비 비중을 다소 낮춤 (공격 유도)
            }
            else if (piece.Type == PieceType.Advisor)
            {
                // 사는 궁성 내에 머무르는 것이 좋음
                int centerRow = isHan ? 8 : 1;
                if (pos.Col >= 3 && pos.Col <= 5 && Math.Abs(pos.Row - centerRow) <= 1)
                    bonus += 5;
            }
            // 2. 공격 기물 위치 평가
            else
            {
                // 중앙(Col 3,4,5) 장악 보너스
                if (pos.Col >= 3 && pos.Col <= 5)
                {
                    bonus += 10; // 중앙 장악 중요도 상향
                }

                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        // 졸/병은 전진할수록, 그리고 뭉쳐있을수록 가치가 올라감
                        if (!isHan)
                            bonus += pos.Row * 3; // 위로 전진 가중치 상향
                        else
                            bonus += (9 - pos.Row) * 3;
                        
                        // 졸끼리 인접해 있으면 수비/공격 보너스 (Linked Pawns)
                        var myPieces = board.GetPiecesBySide(piece.Side);
                        foreach (var p in myPieces)
                        {
                            if (p != piece && p.Type == PieceType.Pawn)
                            {
                                if (Math.Abs(p.Position.Col - pos.Col) <= 1 && Math.Abs(p.Position.Row - pos.Row) <= 1)
                                {
                                    bonus += 3;
                                }
                            }
                        }
                        break;

                    case PieceType.Horse:
                    case PieceType.Elephant:
                        // 마/상은 중앙 진출 시 활약도 증가 (Row 3~6)
                        if (pos.Row >= 3 && pos.Row <= 6)
                            bonus += 15; // 공격적 진출 보너스 상향
                        
                        // 가장자리에 있으면 감점 (이동 반경 제한)
                        if (pos.Col == 0 || pos.Col == 8)
                            bonus -= 5;
                        break;

                    case PieceType.Chariot:
                        // 차는 적 진영 깊숙이 침투 시 높은 점수 (적 궁성 위협)
                        if (isHan && pos.Row <= 3)
                            bonus += 30; // 침투 보너스 대폭 상향
                        else if (!isHan && pos.Row >= 6)
                            bonus += 30;
                        
                        // 차가 개방된 열(앞에 기물이 없는 열)에 있으면 보너스
                        bonus += 10;
                        break;

                    case PieceType.Cannon:
                        // 포는 궁성 주변 조준 또는 중앙 조준 시 가치 상승
                        if (pos.Col >= 3 && pos.Col <= 5)
                            bonus += 15; // 공격적 조준 보너스 상향
                        
                        // 포는 아군 진영에 있을 때 방어 포대로서 가치가 높음
                        if ((isHan && pos.Row >= 6) || (!isHan && pos.Row <= 3))
                            bonus += 3; // 수비적 위치는 상대적으로 가치 하락
                        break;
                }
            }

            // 상대 소환 구역 점거(스폰 블로킹) 보너스
            if (piece.Type != PieceType.King && piece.Type != PieceType.Advisor && IsBlockingOpponentSpawn(piece))
            {
                bonus += 20; // 스폰 블로킹 보너스 대폭 상향 (공격 압박 유도)
            }

            return bonus;
        }

        /// <summary>
        /// 해당 기물이 상대방의 핵심 소환 구역(4선, 1선, 포진지)을 점거(스폰 블로킹)하고 있는지 판정합니다.
        /// </summary>
        public static bool IsBlockingOpponentSpawn(Piece piece)
        {
            var pos = piece.Position;
            if (piece.Side == PlayerSide.Han)
            {
                // 한(AI) 기물이 초(플레이어) 소환 구역에 위치하는지
                if (pos.Row == 3) return true; // 초의 졸 소환선 (4선)
                if (pos.Row == 0) return true; // 초의 차/마/상 소환선 (1선)
                if (pos == new BoardPosition(1, 2) || pos == new BoardPosition(7, 2)) return true; // 초 포진지
            }
            else
            {
                // 초 기물이 한 소환 구역에 위치하는지
                if (pos.Row == 6) return true; // 한의 졸 소환선 (6선)
                if (pos.Row == 9) return true; // 한의 차/마/상 소환선 (9선)
                if (pos == new BoardPosition(1, 7) || pos == new BoardPosition(7, 7)) return true; // 한 포진지
            }

            return false;
        }
    }
}
