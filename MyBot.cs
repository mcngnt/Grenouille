using ChessChallenge.API;
using System;
using System.Linq;


public class MyBot : IChessBot
{

    int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };


    int[,] tables = {{0,  0,  0,  0,
                        50, 50, 50, 50,
                        10, 10, 20, 30,
                         5,  5, 10, 25,
                         0,  0,  0, 20,
                         5, -5,-10,  0,
                         5, 10, 10,-20,
                         0, 0,  0,   0},
                        { -50,-40,-30,-30,
                            -40,-20,  0,  0,
                            -30,  0, 10, 15,
                            -30,  5, 15, 20,
                            -30,  0, 15, 20,
                            -30,  5, 10, 15,
                            -40,-20,  0,  5,
                            -50,-40,-30,-30},

                        {-20,-10,-10,-10,
                          -10,  0,  0,  0,
                          -10,  0,  5, 10,
                          -10,  5,  5, 10,
                          -10,  0, 10, 10,
                          -10, 10, 10, 10,
                          -10,  5,  0,  0,
                          -20,-10,-10,-10},

                        { 0,  0,  0,  0,
                          5, 10, 10, 10,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                         -5,  0,  0,  0,
                          0,  0,  0,  5},

                        { -20,-10,-10,-5,
                          -10,  0,  0,  0,
                          -10,  0,  5,  5,
                           -5,  0,  5,  5,
                            0,  0,  5,  5,
                          -10,  5,  5,  5,
                          -10,  0,  5,  0,
                          -20,-10,-10, -5},

                        {-30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -30,-40,-40,-50,
                        -20,-30,-30,-40,
                        -10,-20,-20,-20,
                         20, 20,  0,  0,
                         20, 30, 10,  0} };

    Move bestMove;
    Move secondBestMove;
    bool hasNotFinished;
    int maxTime = 200;
    //int posNB;
    Move finalMove;
    Move secondFinalMove;


    /*public void SetTransposition(ulong key, int value, Move bmove, byte depth, byte nodeType)
    {
        int index = (int)(key % (ulong)tableSize);
        table[index] = Transposition(key, value, bmove, depth, nodeType);
    }*/

    //public Transposition GetTransposition()
    //{

    //}


    public Move Think(Board board, Timer timer)
    {

        //Console.WriteLine(Eval(Board.CreateBoardFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")));

        float lastEval = 0;

        int finalDepth = 0;

        ///Console.WriteLine("*------*");

        for (int i = 1; i <= 50; i++)
        {
            hasNotFinished = false;
            //posNB = 0;
            float eval = Search(board, float.NegativeInfinity, float.PositiveInfinity, i, i, false, timer);

            if (!hasNotFinished)
            {
                //Console.WriteLine(posNB);
                lastEval = eval;
                finalMove = bestMove;
                secondFinalMove = secondBestMove;
                finalDepth += 1;
            }
            else
            {
                break;
            }
        }

        //Console.WriteLine("------");

        //Console.WriteLine(lastEval);
        //Console.WriteLine(finalDepth);

        //Console.WriteLine("*------*");

        board.MakeMove(finalMove);
        if (board.IsDraw() && lastEval > -50 && !secondFinalMove.Equals(Move.NullMove))
        {
            board.UndoMove(finalMove);
            return secondFinalMove;
        }
        board.UndoMove(finalMove);

        return finalMove;
    }

    public float Search(Board board, float alpha, float beta, int depth, int startingDepth, bool isQuiet, Timer timer)
    {

        if (!isQuiet)
        {
            if (depth == 0)
            {
                //posNB += 1;
                return Search(board, alpha, beta, depth, startingDepth, true, timer);
            }

            if (board.IsInCheckmate())
            {
                return -20000;
            }

            if (timer.MillisecondsElapsedThisTurn > maxTime)
            {
                hasNotFinished = true;
                return 0;
            }
        }
        else
        {
            float currentEval = Eval(board);
            if (currentEval >= beta)
            {
                return beta;
            }
            if (currentEval > alpha)
            {
                alpha = currentEval;
            }
        }


        Move[] moves = board.GetLegalMoves(isQuiet);

        if (!isQuiet)
        {


            float[] scores = moves.Select(move => ((move.Equals(finalMove) ? 9999f : 0f) + (move.IsPromotion ? pieceValues[(int)move.PromotionPieceType] : 0f) + (move.IsCapture ? (pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType]) : 0))).ToArray();

            Move[] movesCopy = new Move[moves.Length];

            Array.Copy(moves, movesCopy, moves.Length);

            int comp(Move move1, Move move2)
            {
                return scores[Array.IndexOf(movesCopy, move1)].CompareTo(scores[Array.IndexOf(movesCopy, move2)]);
            }

            Array.Sort(moves, comp);
        }


        Move currentBestMove = Move.NullMove;
        Move secondCurrentBestMove = Move.NullMove;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            float eval = -Search(board, -beta, -alpha, depth - 1, startingDepth, isQuiet, timer);
            board.UndoMove(move);

            if (eval >= beta)
            {
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
                secondCurrentBestMove = currentBestMove;
                currentBestMove = move;
            }
        }

        if (!isQuiet && depth == startingDepth)
        {
            bestMove = currentBestMove;
            secondBestMove = secondCurrentBestMove;
        }


        return alpha;

    }
    public float Eval(Board board)
    {
        float score = 0;

        PieceList[] plists = board.GetAllPieceLists();

        int pieceNumber = 0;

        foreach (PieceList plist in plists)
        {
            pieceNumber += plist.Count;
        }
        // 0 at first and 1 in endgame
        float endGameCoef = 1 - (pieceNumber / 32);

        foreach (PieceList plist in plists)
        {
            for (int i = 0; i < plist.Count; i++)
            {
                Piece p = plist.GetPiece(i);

                int colorMult = board.IsWhiteToMove == p.IsWhite ? 1 : -1;

                score += pieceValues[(int)p.PieceType] * colorMult * 3 + tables[(int)p.PieceType - 1, (p.Square.File >= 4 ? 7 - p.Square.File : p.Square.File) + 4 * (p.IsWhite ? 7 - p.Square.Rank : p.Square.Rank)] * colorMult * (1 - endGameCoef);

                if (p.PieceType == PieceType.King)
                {
                    score -= (Math.Abs(p.Square.File - 3) + Math.Abs(p.Square.Rank - 3)) * endGameCoef * colorMult * 10;
                }

            }
        }

        return score;
    }
}