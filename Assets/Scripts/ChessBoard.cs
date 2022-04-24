using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum SpecialMoves
{
    None = 0 ,
    Enpassent,
    castling,
    promotion
}

public class ChessBoard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMatrial;
    [SerializeField] private float  tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject VictoryScreen;
    

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //logic
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private bool isWhiteTurn;
    private SpecialMoves specialMoves;
    private void Awake()
    {
        isWhiteTurn = true;
        GenerateAllTiles(tileSize,TILE_COUNT_X,TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();
    }
   
    private void Update()
    {
        if (!currentCamera )
        {
            currentCamera = Camera.main;
            return;
        }
        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray , out info , 100 , LayerMask.GetMask("Tile","Hover","Highlight")))
        {
            // Get the indexes of the tile i have hit
            Vector2Int hitPostion = LookupTileIndex(info.transform.gameObject);

            //hovering a tile after nothing 
            if(currentHover == -Vector2Int.one)
            {
                currentHover = hitPostion;
                tiles[hitPostion.x, hitPostion.y].layer = LayerMask.NameToLayer("Hover");
            }
            // if already hovering 
            if (currentHover != hitPostion)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsVaildMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPostion;
                tiles[hitPostion.x, hitPostion.y].layer = LayerMask.NameToLayer("Hover");
            }

            if(Input.GetMouseButtonDown(0)) //press on
            {
                if (chessPieces[hitPostion.x, hitPostion.y] != null)
                {
                    //my turn
                    if ((chessPieces[hitPostion.x, hitPostion.y].team==0 && isWhiteTurn)|| (chessPieces[hitPostion.x, hitPostion.y].team == 1 && !isWhiteTurn))
                    {
                        currentlyDragging = chessPieces[hitPostion.x, hitPostion.y];
                         availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y); //list of where i can go ,,,, highlight tiles to go
                        // special moves 
                        specialMoves = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                        PreventCheck();
                        HightlightTiles();
                    }
                }
            }
            if (currentlyDragging!=null && Input.GetMouseButtonUp(0)) // releasing the mouse
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool vaildMove = Moveto(currentlyDragging, hitPostion.x, hitPostion.y);
                if (!vaildMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y)) ;
                    currentlyDragging = null;
                }
                else
                {
                    currentlyDragging = null;
                    RemoveHightlightTiles();

                }

            }
           
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer =(ContainsVaildMove(ref availableMoves , currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }
            if (currentlyDragging  && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));

                currentlyDragging = null;

                RemoveHightlightTiles();


            }
        }

        // Dragging a piece

        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up , Vector3.up * yOffset);
            float distance = 0.0f;
            if(horizontalPlane.Raycast(ray , out distance))
            {
               currentlyDragging.SetPosition(  ray.GetPoint(distance) + Vector3.up *dragOffset);
            }
        }
    }

  

    // Generate the board
    private void GenerateAllTiles(float tileSize , int tileCountX , int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (TILE_COUNT_X / 2) * tileSize) + boardCenter;



        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)

            for (int y = 0; y < tileCountY; y++)

                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            
        
    }

    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}",x,y));
        tileObject.transform.parent = transform; //moving tiles

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material= tileMatrial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;

        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x+1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] tris = new int[] { 0,1,2,1,3,2};
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();
        return tileObject;

    }

    //spwaning

    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        int whiteTeam = 0 , blackTeam = 1;


        //white
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);

        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
        }
        

        //black

        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);

       for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
        }
        

    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type , int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null)
                {
                    PositionSinglePiece(x, y, true);
                }
            }

        }
    }

    private void PositionSinglePiece(int x , int y , bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y),force ) ;
    }
    private Vector3 GetTileCenter(int x,int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }

    // checkMate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        VictoryScreen.SetActive(true);
        VictoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnResetButton()
    {
        // UI
        VictoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        VictoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        VictoryScreen.SetActive(false);
        // fields reset 
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        // clean up
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y]!=null)
                {
                    Destroy(chessPieces[x,y].gameObject);
                }
                chessPieces[x, y] = null;

            }

        }
        for (int i = 0; i < deadWhites.Count; i++)
        {
            Destroy(deadWhites[i].gameObject);
        }
        for (int i = 0; i < deadBlacks.Count; i++)
        {
            Destroy(deadBlacks[i].gameObject);
        }
        deadWhites.Clear();
        deadBlacks.Clear();
        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;

    }
    public void OnExitButton()
    {
        Application.Quit();
    }

    // special moves
    private void ProcessSpecialMove()
    {
        if(specialMoves==SpecialMoves.Enpassent)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            if(myPawn.currentX == enemyPawn.currentX)
            {
                if(myPawn.currentY==enemyPawn.currentY -1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if(enemyPawn.team==0)
                    {
                        
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2) +
                            (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2) +
                            (Vector3.forward * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }
        if(specialMoves== SpecialMoves.promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];
            if(targetPawn.type==ChessPieceType.Pawn)
            {
                //white
                if(targetPawn.team==0 && lastMove[1].y==7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                //balck
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }
        if(specialMoves==SpecialMoves.castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            // left
            if (lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0) // white
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0]= null;
                }
                else if(lastMove[1].y==7) // balck
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;

                }
            }
            // right
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0) // white
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePiece(5,0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7) // balck
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;

                }
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null)
                    if (chessPieces[x, y].type == ChessPieceType.King)
                        if (chessPieces[x, y].team == currentlyDragging.team)
                            targetKing = chessPieces[x, y];

            }

        }
        // to help us to know the checkmate
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp , ref List<Vector2Int> moves , ChessPiece TargetKing )
    {
        //save the current value , to reset after call 
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesRemoved = new List<Vector2Int>();
        // simulate all moves and check 
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;
            Vector2Int KingPosInThisSim = new Vector2Int(TargetKing.currentX, TargetKing.currentY);
            // is the kingMove simulated ????
            if(cp.type ==  ChessPieceType.King)
            {
                KingPosInThisSim = new Vector2Int(simX, simY);
            }
            // just copy the pos 
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttacking = new List<ChessPiece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if(chessPieces[x,y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simAttacking.Add(simulation[x, y]);
                    }

                }

            }
            // sim the Move 
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;
           
            // did any piece died during sim  x balck y white error in white using || , && bothe error
            var deadPiece = simAttacking.Find(c => c.currentX == simX && c.currentY == simY );
            if (deadPiece != null)
                simAttacking.Remove(deadPiece);

            // get all simukated attacking pieces Moves
            List<Vector2Int> simMove = new List<Vector2Int>();
            for (int a = 0; a < simAttacking.Count; a++)
            {
                var pieceMoves = simAttacking[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMove.Add(pieceMoves[b]);

                }

            }
            // is the king in danger !? remove the move 
            if(ContainsVaildMove(ref simMove, KingPosInThisSim))
            {
                movesRemoved.Add(moves[i]);
            }
            // restore the actual data to CP
            cp.currentX = actualX;
            cp.currentY = actualY;
        }
        // remove from current available if it check move
        for (int i = 0; i < movesRemoved.Count; i++)
        {
            moves.Remove(movesRemoved[i]);

        }

    }
    private bool CheckForCheckMate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team== 0 )? 1 : 0;
        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> DEFPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    
                   if (chessPieces[x, y].team == targetTeam)
                    {
                        DEFPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];

                    }
                   else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                    
                }
                   

            }
        }
        // is the king attacked now ??
        List<Vector2Int> currentAvailableMove = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailableMove.Add(pieceMoves[b]);

            }
        }
        // are we in check now?
        if(ContainsVaildMove(ref currentAvailableMove, new Vector2Int(targetKing.currentX,targetKing.currentY)))
        {
            for (int i = 0; i < DEFPieces.Count; i++)
            {
                List<Vector2Int> defMoves = DEFPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMoveForSinglePiece(DEFPieces[i], ref defMoves, targetKing);
                if (defMoves.Count != 0)
                    return false;
            }
            return true; // checking exit
        }
            
        return false;
    }
    // Operations
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
            }
        }
        return -Vector2Int.one; // not found btw
    }
    private bool Moveto(ChessPiece cp, int x, int y)
    {
        if (!ContainsVaildMove(ref availableMoves , new Vector2Int(x,y)))
        {
            return false;
        }

        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);
        // is there another piece on the target position ???

        if(chessPieces[x,y]!= null)
        {
            ChessPiece othercp = chessPieces[x, y];
            if(cp.team == othercp.team)
            {
                return false;
            }
            // if its enemy team

            if(othercp.team == 0)
            {
                if (othercp.type == ChessPieceType.King)
                    CheckMate(1);
                deadWhites.Add(othercp);
                othercp.SetScale(Vector3.one * deathSize);
                othercp.SetPosition(new Vector3(8*tileSize,yOffset,-1*tileSize)
                    - bounds 
                    + new Vector3(tileSize/2 , 0 ,tileSize/2) +
                    (Vector3.forward * deathSpacing)* deadWhites.Count);
            }
            else
            {
                if (othercp.type == ChessPieceType.King)
                    CheckMate(0);
                deadBlacks.Add(othercp);
                othercp.SetScale(Vector3.one * deathSize);
                othercp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                  - bounds
                  + new Vector3(tileSize / 2, 0, tileSize / 2) +
                  (Vector3.back * deathSpacing) * deadBlacks.Count);
            }

        }
        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);
        isWhiteTurn = !isWhiteTurn;
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        ProcessSpecialMove();
        if (CheckForCheckMate())
            CheckMate(cp.team);
        return true;
    }
    private bool ContainsVaildMove(ref List<Vector2Int> moves , Vector2 pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if(moves[i].x == pos.x && moves[i].y ==pos.y)
            {
                return true;
            }

        }
        return false;
    }
    //highlight

    private void  HightlightTiles ()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHightlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }
}
