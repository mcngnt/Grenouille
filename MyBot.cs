using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class MyBot : IChessBot
{

    private record struct TTEntry(ulong hash, Move bestMove, int score, int depth, int flag);
    private readonly TTEntry[] transpositionTable = new TTEntry[0x400000];

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
    Move[] killerMoves = new Move[100];
    int maxTime = 100;
    Board board;
    Timer timer;
    int nodes;

    public Move Think(Board newBoard, Timer newTimer)
    {
        board = newBoard;
        timer = newTimer;


        maxTime = timer.MillisecondsRemaining / 30;

        


        for (int i = 1; i <= 60; i++)
        {
            nodes = 0;
            float eval = Search(-999999, 999999, i, 0, true);
            if (timer.MillisecondsElapsedThisTurn > maxTime)
            {
                break;
            }
            Console.WriteLine("Depth : " + i + "  ||  Eval : " + eval + "  ||  Nodes : " + nodes);
        }

        return bestMove;
    }

    public int Search(int alpha, int beta, int depth, int plyFromRoot, bool canNull)
    {
        nodes++;


        //Console.WriteLine("Ply : " + plyFromRoot + " Depth : " + depth);


        ref TTEntry entry = ref transpositionTable[board.ZobristKey & 0x3FFFFF];
        int flag = entry.flag;

        if (entry.hash == board.ZobristKey && depth > 0 && entry.depth >= depth && (flag == 1 || flag == 2 && entry.score <= alpha || flag == 3 && entry.score >= beta))
        {
            return entry.score;
        }

        int startingAlpha = alpha;

        if (depth > 0)
        {
            if(timer.MillisecondsElapsedThisTurn > maxTime)
            {
                return 999999;
            }

            if (board.IsDraw())
            {
                return 0;
            }

            if (board.IsInCheckmate())
            {
                return -10000;
            }

            if (!board.IsInCheck() && canNull && depth >= 3)
            {
                board.TrySkipTurn();
                int eval = -Search(-beta, -alpha, depth - 3, plyFromRoot + 1, false);
                board.UndoSkipTurn();

                if (eval >= beta)
                {
                    return eval;
                }
            }

        }
        else
        {
         
            int currentEval = 0;

            foreach (PieceList plist in board.GetAllPieceLists())
            {
                for (int i = 0; i < plist.Count; i++)
                {
                    Piece p = plist.GetPiece(i);

                    currentEval += (pieceValues[(int)p.PieceType] * 26 + (tables[((int)p.PieceType - 1) * 32 + (p.Square.File >= 4 ? 7 - p.Square.File : p.Square.File) + 4 * (p.IsWhite ? 7 - p.Square.Rank : p.Square.Rank)] - 50) * 6) * (p.IsWhite == board.IsWhiteToMove ? 1 : -1);

                }
            }

            if (currentEval >= beta)
            {
                return beta;
            }
            if (currentEval > alpha)
            {
                alpha = currentEval;
            }
        }


        /*Move[] moves = board.GetLegalMoves(depth <= 0);

        float score(Move move)
        {
            return (bestMove == move ? 99999 : 0) + (depth > 0 && killerMoves[plyFromRoot] == move ? 9999 : 0) + (move.IsPromotion ? 900 : 0) + (move.IsCapture ? (pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType]) : 0);
        }

        int comp(Move move1, Move move2)
        {
            return -score(move1).CompareTo(score(move2));
        }

        Array.Sort(moves, comp);

*/
        Span<Move> moves = stackalloc Move[218];
        board.GetLegalMovesNonAlloc(ref moves, depth > 0 && !board.IsInCheck());
        Span<int> scores = stackalloc int[moves.Length];
        int movesScoreIter = 0;
        foreach (Move move in moves)
        {
            scores[movesScoreIter++] = -(entry.bestMove == move ? 999999 : move.IsCapture ? 99999 * (pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType]) : killerMoves[plyFromRoot] == move ? 9999 : 0);
        }
        scores.Sort(moves);



        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(-beta, -alpha, depth - 1 + (board.IsInCheck() ? 1 : 0), plyFromRoot + 1, canNull);
            board.UndoMove(move);


            if (eval >= beta)
            {
                if (depth > 0 && !move.IsCapture)
                {
                    killerMoves[plyFromRoot] = move;
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

        entry = new(
                board.ZobristKey,
                bestMove,
                (int)alpha,
                depth,
                alpha >= beta ? 3 : alpha <= startingAlpha ? 2 : 1);

        return alpha;

    }


}