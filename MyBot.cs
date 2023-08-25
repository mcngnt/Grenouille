using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    record struct Entry(ulong zobristHash, int score, int depth, Move bestMove, int flag);
    readonly Entry[] transpositionTable = new Entry[4000000];

    Move rootMove;
    int maxTime;
    Board board;
    Timer timer;

#if DEBUG
    int nodes;
#endif


    readonly int[] pieceValues = { 82, 337, 365, 477, 1025, 0,
                                   94, 281, 297, 512, 936, 0 };
    readonly int[] piecePhaseValue = { 0, 1, 1, 2, 4, 0 };

    Move[,] killerMoves = new Move[99, 64];

    int[,] historyHeuristicTable;

    private readonly int[] scores = new int[250];


    readonly decimal[] packedPieceTable = { 64365675561202951673254613248m, 72128178712990387091628344576m, 75532537563137722899854125312m, 75536154932036771593335594752m, 77083570536266386456057948416m, 3110608541636285942974430976m, 936945656906376365998470656m, 75839562049893511208233580800m, 77048506965452586199286796097m, 3420098330133489500069553497m, 2810795141387667142788526121m, 3437013754922314846191425599m, 3452687399601669993672216365m, 7757691002792640473062648148m, 4666481959896172496725738775m, 2166409085788340820470003193m, 2460187690506760228122780156m, 3409199070807303941323827205m, 4649552314590726507357608209m, 3134763521725202195292039957m, 4060800717490095119635265579m, 9313555161389368408616818213m, 8991976096719656293972390161m, 2793818196253891283677814259m, 77683174187029576759828675319m, 4660418590176712645448764169m, 4971145620211324499469864196m, 5607002785501568496010601230m, 5307189237648302219852323087m, 6841316065198388299381157384m, 5308388588818498857642298379m, 647988997712072446081699825m, 75809334407291471090360842222m, 78322691297526401051434484735m, 4348529951871323093202439165m, 4989251265752579450371901704m, 5597312470813537077508379403m, 4671270607515738602000092420m, 1889532129915238800511405575m, 77081081831403468769999453167m, 75502243563272129380224135663m, 78896921543467231770095319805m, 2179679196345332391270287613m, 4338830174078735659142088697m, 4650714182750414584320429314m, 3418804494205898040108256002m, 1557077491546032522931212566m, 77376040767919248347220145656m, 73949978069138388163940838889m, 77354313122912694185105219071m, 1213766170969874494391056627m, 3081561358716687252805713649m, 3082732579536108768113065974m, 1220991372808161450603187216m, 78581358699470342360447514393m, 76109128861678447697508561905m, 68680260866671043956521482752m, 72396532057671382741144236544m, 75186737407056809798802069760m, 77337402494773235399328459264m, 73655004947793353634062267648m, 76728065955037551248392383744m, 74570190181483732716705215232m, 70531093311992994451646771456m };

    readonly int[,] pieceTable = new int[12, 64];


    public MyBot()
    {
        for (int square = 0; square < 64; square++)
            for (int tableID = 0; tableID < 12; tableID++)
                pieceTable[tableID, square] = (int)Math.Round((sbyte)((System.Numerics.BigInteger)packedPieceTable[square]).ToByteArray()[tableID] * 1.5) + pieceValues[tableID];
    }

    public Move Think(Board newBoard, Timer newTimer)
    {
        board = newBoard;
        timer = newTimer;

        maxTime = timer.MillisecondsRemaining / 30;


        historyHeuristicTable = new int[7, 64];

        for (int d = 2, alpha = -999999, beta = 999999; d < 90;)
        {
#if DEBUG
            nodes = 0;
#endif
            int eval = Search(alpha, beta, d, 0, true);
            if (timer.MillisecondsElapsedThisTurn > maxTime)
                break;

#if DEBUG
            Console.WriteLine("info Depth : " + d + "  ||  Eval : " + eval + "  ||  Nodes : " + nodes + " || Best Move : " + rootMove.StartSquare.Name + rootMove.TargetSquare.Name);
#endif

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

        }
        return rootMove;
    }


    public int Search(int alpha, int beta, int depth, int plyFromRoot, bool allowNullMove)
    {
#if DEBUG
        nodes++;
#endif
        ref Entry entry = ref transpositionTable[board.ZobristKey & 3999999];

        bool isQuiescence = depth <= 0, isCheck = board.IsInCheck(), canPrune = false;
        int bestEval = -999999, startingAlpha = alpha, moveCount = 0, eval = 0, scoreIter = 0, entryScore = entry.score, entryFlag = entry.flag;
        Move bestMove = default;

        int LambdaSearch(int alphaBis, bool allowNull, int R = 1) => eval = -Search(-alphaBis, -alpha, depth - R, plyFromRoot + 1, allowNull);

        if (board.IsDraw())
            return 0;

        if (board.IsInCheckmate())
            return plyFromRoot - 999999;


        if (plyFromRoot > 0 && entry.zobristHash == board.ZobristKey && entry.depth >= depth && (entryFlag == 1 || entryFlag == 2 && entryScore <= alpha || entryFlag == 3 && entryScore >= beta))
            return entryScore;

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
        else if (!isCheck && beta - alpha == 1)
        {

            canPrune = depth < 9 && Evaluate() + depth * 140 <= alpha;

            if (allowNullMove && depth > 2)
            {
                board.TrySkipTurn();
                LambdaSearch(beta, allowNullMove, 3);
                board.UndoSkipTurn();

                if (eval > beta)
                    return beta;

            }

        }


        Span<Move> moves = stackalloc Move[218];
        board.GetLegalMovesNonAlloc(ref moves, isQuiescence && !isCheck);

        foreach (Move move in moves)
            scores[scoreIter++] = -(move == entry.bestMove ? 9000000 : move.IsCapture ? 1000000 * (int)move.CapturePieceType - (int)move.MovePieceType : killerMoves[plyFromRoot, move.TargetSquare.Index] == move ? 900000 : historyHeuristicTable[(int)move.MovePieceType, move.TargetSquare.Index]);

        scores.AsSpan(0, moves.Length).Sort(moves);


        foreach (Move move in moves)
        {

            if (canPrune && moveCount > 0 && !move.IsCapture)
                continue;

            board.MakeMove(move);
            if (moveCount++ == 0 || isQuiescence)
                LambdaSearch(beta, allowNullMove);
            else
            {
                if (moveCount >= 5 && depth >= 2)
                    LambdaSearch(alpha + 1, allowNullMove, 3);
                else
                    eval = alpha + 1;

                if (eval > alpha)
                {
                    LambdaSearch(alpha + 1, allowNullMove);
                    if (eval > alpha)
                        LambdaSearch(beta, allowNullMove);
                }
            }

            board.UndoMove(move);

            if (eval > bestEval)
            {

                bestEval = eval;


                if (eval > alpha)
                {
                    bestMove = move;
                    alpha = eval;
                    if (plyFromRoot == 0)
                        rootMove = bestMove;
                }


                if (alpha >= beta)
                {
                    if (!move.IsCapture)
                    {
                        killerMoves[plyFromRoot, move.TargetSquare.Index] = move;
                        historyHeuristicTable[(int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                    }
                    break;
                }

            }

            if (depth > 2 && timer.MillisecondsElapsedThisTurn > maxTime)
                return 999999;
        }


        entry = new(board.ZobristKey, bestEval, depth, bestMove == default ? entry.bestMove : bestMove, bestEval >= beta ? 3 : bestEval <= startingAlpha ? 2 : 1);

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
                        squareIndex ^= 56;

                    gamePhase += piecePhaseValue[pieceID];
                    middleGame += pieceTable[pieceID, squareIndex];
                    endGame += pieceTable[pieceID + 6, squareIndex];
                }

            }
        }

        return (middleGame * gamePhase + endGame * (24 - gamePhase)) / 24 * (board.IsWhiteToMove ? 1 : -1) + gamePhase / 2;

    }

}