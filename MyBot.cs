using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    record struct Entry(ulong zobristHash, int score, int depth, Move bestMove, int flag);
    Entry[] transpositionTable = new Entry[4000000];

    Move rootMove;
    int maxTime = 200;
    Board board;
    Timer timer;
    int nodes;

    int[] pieceValues = { 82, 337, 365, 477, 1025, 0,
                          94, 281, 297, 512, 936, 0 };
    int[] piecePhaseValue = { 0, 1, 1, 2, 4, 0 };

    Move[,] killerMoves;

    int[,] historyHeuristicTable;


    decimal[] packedPieceTable = { 64365675561202951673254613248m, 72128178712990387091628344576m, 75532537563137722899854125312m, 75536154932036771593335594752m, 77083570536266386456057948416m, 3110608541636285942974430976m, 936945656906376365998470656m, 75839562049893511208233580800m, 77048506965452586199286796097m, 3420098330133489500069553497m, 2810795141387667142788526121m, 3437013754922314846191425599m, 3452687399601669993672216365m, 7757691002792640473062648148m, 4666481959896172496725738775m, 2166409085788340820470003193m, 2460187690506760228122780156m, 3409199070807303941323827205m, 4649552314590726507357608209m, 3134763521725202195292039957m, 4060800717490095119635265579m, 9313555161389368408616818213m, 8991976096719656293972390161m, 2793818196253891283677814259m, 77683174187029576759828675319m, 4660418590176712645448764169m, 4971145620211324499469864196m, 5607002785501568496010601230m, 5307189237648302219852323087m, 6841316065198388299381157384m, 5308388588818498857642298379m, 647988997712072446081699825m, 75809334407291471090360842222m, 78322691297526401051434484735m, 4348529951871323093202439165m, 4989251265752579450371901704m, 5597312470813537077508379403m, 4671270607515738602000092420m, 1889532129915238800511405575m, 77081081831403468769999453167m, 75502243563272129380224135663m, 78896921543467231770095319805m, 2179679196345332391270287613m, 4338830174078735659142088697m, 4650714182750414584320429314m, 3418804494205898040108256002m, 1557077491546032522931212566m, 77376040767919248347220145656m, 73949978069138388163940838889m, 77354313122912694185105219071m, 1213766170969874494391056627m, 3081561358716687252805713649m, 3082732579536108768113065974m, 1220991372808161450603187216m, 78581358699470342360447514393m, 76109128861678447697508561905m, 68680260866671043956521482752m, 72396532057671382741144236544m, 75186737407056809798802069760m, 77337402494773235399328459264m, 73655004947793353634062267648m, 76728065955037551248392383744m, 74570190181483732716705215232m, 70531093311992994451646771456m };

    int[,] pieceTable = new int[12, 64];


    public MyBot()
    {
        for (int square = 0; square < 64; square++)
        {
            for (int tableID = 0; tableID < 12; tableID++)
            {
                pieceTable[tableID, square] = (int)Math.Round((sbyte)((System.Numerics.BigInteger)packedPieceTable[square]).ToByteArray()[tableID] * 1.5) + pieceValues[tableID];
            }
        }

    }

    public Move Think(Board newBoard, Timer newTimer)
    {
        board = newBoard;
        timer = newTimer;

        //maxTime = timer.MillisecondsRemaining / 30;

        killerMoves = new Move[99, 64];

        historyHeuristicTable = new int[7, 64];

        int alpha = -999999;
        int beta = 999999;

        for (int d = 1; d <= 90;)
        {
            nodes = 0;
            int eval = Search(alpha, beta, d, 0, true);
            if (timer.MillisecondsElapsedThisTurn > maxTime)
                break;

            if (eval <= alpha)
                alpha -= 62;
            else if (eval >= beta)
                beta += 62;
            else
            {
                alpha = eval - 17;
                beta = eval + 17;
                d++;
            }

            Console.WriteLine("Depth : " + d + "  ||  Eval : " + eval + "  ||  Nodes : " + nodes + " || Best Move : " + rootMove.StartSquare.Name + rootMove.TargetSquare.Name);
        }
        return rootMove;
    }



    public int Search(int alpha, int beta, int depth, int plyFromRoot, bool allowNullMove)
    {
        nodes++;
        bool isQuiescence = depth <= 0, isCheck = board.IsInCheck();
        int bestEval = -999999, startingAlpha = alpha, moveCount = 0, eval = 0, scoreIter = 0;
        Move bestMove = Move.NullMove;

        ref Entry entry = ref transpositionTable[board.ZobristKey & 3999999];

        if (plyFromRoot > 0 && entry.zobristHash == board.ZobristKey && entry.depth >= depth && (entry.flag == 1 || entry.flag == 2 && entry.score <= alpha || entry.flag == 3 && entry.score >= beta))
            return entry.score;

        if (isCheck)
            depth++;

        if (isQuiescence)
        {
            bestEval = Evaluate();
            if (bestEval > alpha)
                alpha = bestEval;
            if (alpha >= beta)
                return bestEval;
        }
        else
        {
            if (allowNullMove && !isCheck && depth >= 2 && beta - alpha == 1)
            {
                board.TrySkipTurn();
                eval = -Search(-beta, -beta + 1, depth - 2, plyFromRoot + 1, false);
                board.UndoSkipTurn();

                if (eval > beta)
                    return beta;

            }
        }

        if (board.IsDraw())
        {
            return 0;
        }
        if (board.IsInCheckmate())
        {
            return plyFromRoot - 999999;
        }

        Span<Move> moves = stackalloc Move[218];
        board.GetLegalMovesNonAlloc(ref moves, isQuiescence && !isCheck);

        Span<int> scores = stackalloc int[moves.Length];


        foreach (Move move in moves)
        {
            scores[scoreIter++] = -(move == entry.bestMove ? 9000000 : move.IsCapture ? (move.CapturePieceType - move.MovePieceType) * 1000000 : killerMoves[plyFromRoot, move.StartSquare.Index] == move ? 1000000 : historyHeuristicTable[(int)move.MovePieceType, move.TargetSquare.Index]);
        }

        scores.Sort(moves);


        foreach (Move move in moves)
        {
            board.MakeMove(move);
            if (moveCount++ == 0 || isQuiescence)
                eval = -Search(-beta, -alpha, depth - 1, plyFromRoot + 1, allowNullMove);
            else
            {
                if (moveCount++ > 3 && depth > 2 && !isCheck && !move.IsCapture)
                    eval = -Search(-alpha - 1, -alpha, depth - 2, plyFromRoot + 1, allowNullMove);
                else
                    eval = alpha + 1;

                if (eval > alpha)
                {
                    eval = -Search(-alpha - 1, -alpha, depth - 1, plyFromRoot + 1, allowNullMove);
                    if (eval > alpha)
                        eval = -Search(-beta, -alpha, depth - 1, plyFromRoot + 1, allowNullMove);
                }
            }

            board.UndoMove(move);

            if (eval > bestEval)
            {
                bestMove = move;
                bestEval = eval;

                if (plyFromRoot == 0)
                    rootMove = bestMove;

                if (eval > alpha)
                    alpha = eval;

                if (alpha >= beta)
                {
                    if (!move.IsCapture)
                    {
                        killerMoves[plyFromRoot, move.StartSquare.Index] = move;
                        historyHeuristicTable[(int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                    }
                    break;
                }

            }

            if (timer.MillisecondsElapsedThisTurn > maxTime)
                return 999999;
        }


        entry = new(board.ZobristKey, bestEval, depth, bestMove, bestEval >= beta ? 3 : bestEval <= startingAlpha ? 2 : 1);

        return bestEval;

    }


    public int Evaluate()
    {
        int gamePhase = 0, endGame = 0, middleGame = 0;

        for (int isWhite = 1; isWhite >= 0; middleGame = -middleGame, endGame = -endGame, --isWhite)
        {
            for (int pieceID = 0; pieceID < 6; pieceID++)
            {

                for (ulong pieceMask = board.GetPieceBitboard((PieceType)(pieceID + 1), isWhite > 0); pieceMask != 0;)
                {
                    int squareIndex = BitboardHelper.ClearAndGetIndexOfLSB(ref pieceMask);

                    if (isWhite > 0)
                        squareIndex = (squareIndex % 8) + 8 * (7 - squareIndex / 8);

                    gamePhase += piecePhaseValue[pieceID];
                    middleGame += pieceTable[pieceID, squareIndex];
                    endGame += pieceTable[pieceID + 6, squareIndex];
                }

            }
        }

        return (middleGame * gamePhase + endGame * (24 - gamePhase)) / 24 * (board.IsWhiteToMove ? 1 : -1) + gamePhase / 2;

    }

}