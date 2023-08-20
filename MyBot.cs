using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class MyBot : IChessBot
{

    // None, Pawn, Knight, Bishop, Rook, Queen, King
    int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };

    int[] tables =  {    50,  50,  50,  50,
                        100, 100, 100, 100,
                         60,  60,  70,  80,
                         55,  55,  60,  75,
                         50,  50,  50,  70,
                         55,  45,  40,  50,
                         55,  60,  60,  30,
                         50,  50,  50,  50,

                          0,  10,  20,  20,
                          20,  50,  60,  65,
                          20,  55,  65,  70,
                          20,  50,  65,  70,
                          20,  55,  60,  65,
                          10,  30,  50,  50,
                          10,  30,  50,  55,
                           0,  10,  20,  20,

                         30,  40,  40,  45,
                          40,  50,  50,  50,
                          40,  50,  55,  60,
                          40,  55,  55,  60,
                          40,  50,  60,  60,
                          40,  60,  60,  60,
                          40,  55,  50,  50,
                          30,  40,  40,  45,

                          0,   0,   0,   0,
                         55,  60,  60,  60,
                         45,  50,  50,  50,
                         45,  50,  50,  50,
                         45,  50,  50,  50,
                         45,  50,  50,  50,
                         45,  50,  50,  50,
                          0,   0,   0,  55,

                         30,  40,  40,  45,
                          40,  50,  50,  50,
                          40,  50,  55,  55,
                          45,  50,  55,  55,
                          50,  50,  55,  55,
                          40,  55,  55,  55,
                          40,  50,  55,  50,
                         30,  40,  40,  45,

                         20,  10,  10,   0,
                          40,  30,  30,  30,
                          40,  50,  55,  60,
                          40,  55,  55,  60,
                          40,  50,  60,  60,
                          40,  60,  60,  60,
                          40,  55,  50,  50,
                         30,  20,  20,  15, };

    Move bestMove;
    HashSet<Move>[] killerMoves;
    int maxTime = 100;
    Board board;
    Timer timer;
    int nodes;

    public Move Think(Board newBoard, Timer newTimer)
    {
        board = newBoard;
        timer = newTimer;

        int castleMask = 0;
        bool sideMoving = board.IsWhiteToMove;

        foreach (Move move in board.GameMoveHistory)
        {
            if (move.IsCastles)
            {
                sideMoving = !sideMoving;
                castleMask |= (sideMoving ? 1 : 2);
            }
        }

        killerMoves = new HashSet<Move>[62];

        for (int i = 0; i < 62; i++)
        {
            killerMoves[i] = new HashSet<Move>();
        }

        //maxTime = timer.MillisecondsRemaining / 30;


        for (int i = 1; i <= 60; i++)
        {
            nodes = 0;
            float eval = Search(float.NegativeInfinity, float.PositiveInfinity, i, 0, false, castleMask);
            if (timer.MillisecondsElapsedThisTurn > maxTime)
            {
                break;
            }
            Console.WriteLine("Depth : " + i + "  ||  Eval : " + eval + "  ||  Nodes : " + nodes);
        }

        return bestMove;
    }

    /* Has Castled :  00 | 01 | 10 | 11  -> Back | White */
    public float Search(float alpha, float beta, int depth, int plyFromRoot, bool isQuiescence, int hasCastled)
    {
        nodes++;
        if (!isQuiescence)
        {
            if (depth == 0)
            {
                return Search(alpha, beta, 0, plyFromRoot + 1, true, hasCastled);
            }

            if (board.IsDraw() || timer.MillisecondsElapsedThisTurn > maxTime)
            {
                return 0;
            }

            if (board.IsInCheckmate())
            {
                return -1000;
            }


        }
        else
        {
            float currentEval = (board.IsInCheck() ? -8.8f : 0);

            float endGameCoef = 1 - (BitboardHelper.GetNumberOfSetBits(board.WhitePiecesBitboard | board.BlackPiecesBitboard) / 32);

            ulong[] control = new ulong[2];


            foreach (PieceList plist in board.GetAllPieceLists())
            {
                for (int i = 0; i < plist.Count; i++)
                {
                    Piece p = plist.GetPiece(i);

                    control[p.IsWhite ? 0 : 1] |= BitboardHelper.GetPieceAttacks(p.PieceType, p.Square, board, p.IsWhite);

                    currentEval += (pieceValues[(int)p.PieceType] * 0.26f + (tables[((int)p.PieceType - 1) * 32 + (p.Square.File >= 4 ? 7 - p.Square.File : p.Square.File) + 4 * (p.IsWhite ? 7 - p.Square.Rank : p.Square.Rank)] - 50) * (1 - endGameCoef) * 0.06f - (p.PieceType == PieceType.King ? Math.Abs(p.Square.File - 3) + Math.Abs(p.Square.Rank - 3) * endGameCoef * endGameCoef * 0.95f : 0)) * (board.IsWhiteToMove == p.IsWhite ? 1 : -1);

                }
            }

            currentEval += ((BitboardHelper.GetNumberOfSetBits(control[0]) - BitboardHelper.GetNumberOfSetBits(control[1])) * 3.7f + ((hasCastled >> 1) - (hasCastled % 2)) * 20f) * (board.IsWhiteToMove ? 1 : -1);

            if (currentEval >= beta)
            {
                return beta;
            }
            if (currentEval > alpha)
            {
                alpha = currentEval;
            }
        }


        Move[] moves = board.GetLegalMoves(isQuiescence);

        float score(Move move)
        {
            return (move.Equals(bestMove) ? 99999 : 0) + (!isQuiescence && killerMoves[plyFromRoot].Contains(move) ? 9999 : 0) + (move.IsPromotion | move.IsCastles ? 900 : 0) + (move.IsCapture ? (pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType]) : 0);
        }

        int comp(Move move1, Move move2)
        {
            return -score(move1).CompareTo(score(move2));
        }

        Array.Sort(moves, comp);


        foreach (var move in moves)
        {
            board.MakeMove(move);
            int extension = board.IsInCheck() ? 1 : 0;
            float eval = -Search(-beta, -alpha, depth - 1 + extension, plyFromRoot + 1, isQuiescence, hasCastled | (move.IsCastles ? (board.IsWhiteToMove ? 1 : 2) : 0));
            board.UndoMove(move);

            if (eval >= beta)
            {
                if (!isQuiescence)
                {
                    killerMoves[plyFromRoot].Add(move);
                }
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
                if (plyFromRoot == 0 && timer.MillisecondsElapsedThisTurn < maxTime)
                {
                    bestMove = move;
                }
            }
        }

        return alpha;
    }


}