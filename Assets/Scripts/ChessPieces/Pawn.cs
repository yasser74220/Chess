using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        int direction = (team == 0) ? 1 : -1; 

        // go front "one"
        if (board[currentX,currentY + direction] == null  )
        {
            r.Add(new Vector2Int(currentX, currentY + direction));
          

        }
       
        // "two move"

        if (board[currentX, currentY + direction] == null)
        {
            if (team == 0 && currentY == 1 && board[currentX, currentY + (direction * 2)] == null) // white team
            {
                r.Add(new Vector2Int(currentX, currentY + (direction*2)));

            }
            if (team == 1 && currentY == 6 && board[currentX, currentY + (direction * 2)] == null) // black team
            {
                r.Add(new Vector2Int(currentX, currentY + (direction * 2)));

            }
        }
        //kill 
        if(currentX != tileCountX - 1)
        {
            if(board[currentX+1, currentY + direction] != null && board[currentX +1 , currentY + direction].team != null)
            {
                r.Add(new Vector2Int(currentX+1, currentY + direction));

            }
        }
        if (currentX != 0)
        {
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != null)
            {
                r.Add(new Vector2Int(currentX -1 , currentY + direction));

            }
        }
        return r;
    }
    public override SpecialMoves GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        int direction = (team == 0) ? 1 : -1;
        if ((team == 0 && currentY == 6) || (team == 1 && currentY == 1))
            return SpecialMoves.promotion;
        // En passant
        if(moveList.Count > 0)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            if(board[lastMove[1].x,lastMove[1].y].type == ChessPieceType.Pawn)
            {
                if(Mathf.Abs(lastMove[0].y - lastMove[1].y)==2)
                {
                    if(board[lastMove[1].x,lastMove[1].y].team != team)
                    {
                        if(lastMove[1].y == currentY)
                        {
                            if(lastMove[1].x == currentX -1)
                            {
                                availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                return SpecialMoves.Enpassent;
                            }
                            if(lastMove[1].x == currentX + 1)
                            {
                                availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                return SpecialMoves.Enpassent;
                            }
                        }
                    }
                }
            }
        }
        return SpecialMoves.None;
    }
}
