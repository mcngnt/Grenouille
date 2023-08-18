using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    // None, Pawn, Knight, Bishop, Rook, Queen, King
    int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };

    int[] tables =  {0,  0,  0,  0,
                        50, 50, 50, 50,
                        10, 10, 20, 30,
                        5,  5, 10, 25,
                        0,  0,  0, 20,
                        5, -5,-10,  0,
                        5, 10, 10,-20,
                        0, 0,  0,   0,

                         -50,-40,-30,-30,
                         -30,  0, 10, 15,
                         -30,  5, 15, 20,
                         -30,  0, 15, 20,
                         -30,  5, 10, 15,
                         -40,-20,  0,  0,
                         -40,-20,  0,  5,
                         -50,-40,-30,-30,

                        -20,-10,-10,-10,
                        -10,  0,  0,  0,
                        -10,  0,  5, 10,
                        -10,  5,  5, 10,
                        -10,  0, 10, 10,
                        -10, 10, 10, 10,
                        -10,  5,  0,  0,
                        -20,-10,-10,-10,

                         0,  0,  0,  0,
                         5, 10, 10, 10,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                          0,  0,  0,  5,

                         -20,-10,-10,-5,
                          -10,  0,  0,  0,
                          -10,  0,  5,  5,
                           -5,  0,  5,  5,
                            0,  0,  5,  5,
                          -10,  5,  5,  5,
                          -10,  0,  5,  0,
                          -20,-10,-10, -5,

                        -30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -20,-30,-30,-40,
                        -10,-20,-20,-20,
                         20, 20,  0,  0,
                         20, 30, 10,  0 };

    Move bestMove;
    //bool hasNotFinished;
    //Move finalMove;
    HashSet<Move>[] killerMoves;
    int maxTime = 100;

    public Move Think(Board board, Timer timer)
    {

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


        for (int i = 1; i <= 60; i++)
        {
            killerMoves[i] = new HashSet<Move>();
            //hasNotFinished = false;
            Search(board, float.NegativeInfinity, float.PositiveInfinity, i, i, timer, false, castleMask);
            //finalMove = bestMove;
            if (timer.MillisecondsElapsedThisTurn > maxTime)
            {
                break;
            }
        }

        //return finalMove;
        return bestMove;
    }

    /* Has Castled :  00 | 01 | 10 | 11  -> Back | White */
    public float Search(Board board, float alpha, float beta, int depth, int startingDepth, Timer timer, bool isQuiescence, int hasCastled)
    {

        if (!isQuiescence)
        {
            if (depth == 0)
            {
                return Search(board, alpha, beta, 0, startingDepth, timer, true, hasCastled);
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

                    currentEval += (pieceValues[(int)p.PieceType] * 0.26f + tables[((int)p.PieceType - 1) * 32 + (p.Square.File >= 4 ? 7 - p.Square.File : p.Square.File) + 4 * (p.IsWhite ? 7 - p.Square.Rank : p.Square.Rank)] * pieceValues[(int)p.PieceType] * (1 - endGameCoef) * 0.0003f + (p.PieceType == PieceType.King ? -(Math.Abs(p.Square.File - 3) + Math.Abs(p.Square.Rank - 3)) * endGameCoef * endGameCoef * 0.95f : 0)) * (board.IsWhiteToMove == p.IsWhite ? 1 : -1);

                }
            }

            currentEval += ((BitboardHelper.GetNumberOfSetBits(control[0]) - BitboardHelper.GetNumberOfSetBits(control[1])) * 3.7f + ((hasCastled >> 1) - (hasCastled % 2)) * 5.3f  ) * (board.IsWhiteToMove ? 1 : -1);
            currentEval *= 0.01f;

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
            return (move.Equals(bestMove) ? 99999 : 0) + (!isQuiescence && killerMoves[depth].Contains(move) ? 9999 : 0) + (move.IsPromotion | move.IsCastles ? 900 : 0) + (move.IsCapture ? (pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType]) : 0);
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
            float eval = -Search(board, -beta, -alpha, depth - 1 + extension, startingDepth + extension, timer, isQuiescence, hasCastled | (move.IsCastles ? (board.IsWhiteToMove ? 1 : 2) : 0));
            board.UndoMove(move);

            if (eval >= beta)
            {
                if (!isQuiescence)
                {
                    killerMoves[depth].Add(move);
                }
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
                if (depth == startingDepth && timer.MillisecondsElapsedThisTurn < maxTime)
                {
                    bestMove = move;
                }
            }
        }

        return alpha;

    }


}