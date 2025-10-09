using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 井字棋（带难度选择、撤销回合、AI、胜利高亮、重新开始、统计保存）
/// - Minimax + alpha-beta（可设置深度限制）
public class behavior0 : MonoBehaviour
{
    public int scale = 3;                     // 棋盘规模
    private int[,] matrix;                    // 棋盘状态：0 空，1 玩家，2 AI

    private int height = 50, width = 50;
    private int firstX, firstY;

    private Texture gouImg; // 玩家勾
    private Texture chaImg; // AI 叉

    private GUIStyle style = new GUIStyle();
    private GUIStyle highlightStyle = new GUIStyle();

    // 高亮胜利的格子坐标
    private List<Vector2Int> winningLine = null;

    private struct Move { public int r, c, player; public Move(int rr, int cc, int p) { r = rr; c = cc; player = p; } }
    private Stack<Move> moveHistory = new Stack<Move>();

    // AI 难度
    private const int aiMaxDepth = 10; 

    // 统计 PlayerPrefs keys
    private const string KEY_WIN = "TicTacToe_PlayerWin";
    private const string KEY_LOSS = "TicTacToe_PlayerLoss";
    private const string KEY_DRAW = "TicTacToe_Draw";

    // 临时显示文本
    private string infoText = "轮到你下";


    void Awake()
    {
        gouImg = Resources.Load("勾") as Texture;
        chaImg = Resources.Load("叉") as Texture;

        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 30;

        // 高亮用半透明背景
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(1f, 1f, 0f, 0.4f));
        tex.Apply();
        highlightStyle.normal.background = tex;

        Reset();
    }

    void Reset()
    {
        matrix = new int[scale, scale];
        moveHistory.Clear();
        winningLine = null;
        infoText = "游戏已重置，轮到你下";

        float boardWidth = Screen.width * 0.3f;
        width = height = Mathf.RoundToInt(boardWidth / scale);
        firstX = Screen.width / 2 - scale * width / 2;
        firstY = Screen.height / 2 - scale * height / 2;
    }


    void OnGUI()
    {
        GUI.skin.button.fontSize = 26; 

        // 顶部统计显示
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Label("统计（本地存档）", style);
        int w = PlayerPrefs.GetInt(KEY_WIN, 0);
        int l = PlayerPrefs.GetInt(KEY_LOSS, 0);
        int d = PlayerPrefs.GetInt(KEY_DRAW, 0);
        GUILayout.Label($"玩家胜：{w}  败：{l}  平：{d}", style);
        GUILayout.Label(infoText, style);

        // 清空统计按钮
        if (GUILayout.Button("清空统计"))
        {
            PlayerPrefs.SetInt(KEY_WIN, 0);
            PlayerPrefs.SetInt(KEY_LOSS, 0);
            PlayerPrefs.SetInt(KEY_DRAW, 0);
            PlayerPrefs.Save();
            infoText = "统计已清空";
        }
        GUILayout.EndArea();

        // 绘制棋盘
        for (int i = 0; i < scale; ++i)
        {
            for (int j = 0; j < scale; ++j)
            {
                Rect cellRect = new Rect(firstX + width * j, firstY + height * i, width, height);

                // 如果在胜利线内，先绘制高亮背景
                if (IsCoordInWinningLine(i, j) && winningLine != null)
                    GUI.Box(cellRect, GUIContent.none, highlightStyle);
                else
                    GUI.Box(cellRect, GUIContent.none);

                if (matrix[i, j] == 0)
                {
                    if (GUI.Button(cellRect, "") && win() == 0)
                    {
                        // 玩家落子
                        matrix[i, j] = 1;
                        moveHistory.Push(new Move(i, j, 1));
                        winningLine = null;

                        // AI 自动落子
                        if (win() == 0)
                            AIMove();
                    }
                }
                else if (matrix[i, j] == 1)
                {
                    if (gouImg != null) GUI.DrawTexture(cellRect, gouImg, ScaleMode.ScaleToFit);
                    else GUI.Button(cellRect, "O");
                }
                else if (matrix[i, j] == 2)
                {
                    if (chaImg != null) GUI.DrawTexture(cellRect, chaImg, ScaleMode.ScaleToFit);
                    else GUI.Button(cellRect, "X");
                }
            }
        }

        // 操作按钮：撤回回合 / 重新开始
        int btnW = width * scale / 2;
        int baseX = firstX;
        int btnY = firstY + scale * height + 30;

        if (GUI.Button(new Rect(baseX, btnY, btnW - 4, height), "撤回回合"))
        {
            if (UndoTurn())
            {
                infoText = "已撤回回合";
                winningLine = null;
            }
            else infoText = "无法撤回回合";
        }

        if (GUI.Button(new Rect(baseX + btnW, btnY, btnW - 4, height), "重新开始"))
        {
            Reset();
        }

        // 胜负显示与统计
        int result = win();
        if (result == 1)
        {
            GUI.Label(new Rect(firstX, firstY - height - 10, width * scale, height), "玩家赢了！", style);
            if (winningLine == null) winningLine = GetWinningLine();
            if (!infoText.Contains("玩家赢"))
            {
                infoText = "玩家赢！";
                IncrementStats(KEY_WIN);
            }
        }
        else if (result == 2)
        {
            GUI.Label(new Rect(firstX, firstY - height - 10, width * scale, height), "AI 赢了！", style);
            if (winningLine == null) winningLine = GetWinningLine();
            if (!infoText.Contains("AI 赢"))
            {
                infoText = "AI 赢！";
                IncrementStats(KEY_LOSS);
            }
        }
        else if (result == 3)
        {
            GUI.Label(new Rect(firstX, firstY - height - 10, width * scale, height), "平局", style);
            if (!infoText.Contains("平局"))
            {
                infoText = "平局";
                IncrementStats(KEY_DRAW);
                winningLine = null;
            }
        }
    }

    bool IsCoordInWinningLine(int r, int c)
    {
        if (winningLine == null) return false;
        foreach (var v in winningLine)
            if (v.x == r && v.y == c) return true;
        return false;
    }

    void IncrementStats(string key)
    {
        int v = PlayerPrefs.GetInt(key, 0);
        PlayerPrefs.SetInt(key, v + 1);
        PlayerPrefs.Save();
    }

    // 撤回回合：AI + 玩家一起撤销
    bool UndoTurn()
    {
        if (moveHistory.Count == 0) return false;
        Move last = moveHistory.Pop();
        matrix[last.r, last.c] = 0;

        // 如果 last 是 AI，那么再撤销上一手（玩家）
        if (last.player == 2 && moveHistory.Count > 0)
        {
            Move prev = moveHistory.Pop();
            matrix[prev.r, prev.c] = 0;
        }
        else if (last.player == 1)
        {
            // 如果撤回的是玩家棋子，还没下AI，那就只撤一子
        }

        winningLine = null;
        return true;
    }

    List<Vector2Int> GetWinningLine()
    {
        List<Vector2Int> res = new List<Vector2Int>();
        int first;
        // 横
        for (int i = 0; i < scale; i++)
        {
            first = matrix[i, 0];
            if (first != 0)
            {
                bool ok = true;
                for (int j = 1; j < scale; j++)
                    if (matrix[i, j] != first) { ok = false; break; }
                if (ok)
                {
                    for (int j = 0; j < scale; j++) res.Add(new Vector2Int(i, j));
                    return res;
                }
            }
        }
        // 纵
        for (int j = 0; j < scale; j++)
        {
            first = matrix[0, j];
            if (first != 0)
            {
                bool ok = true;
                for (int i = 1; i < scale; i++)
                    if (matrix[i, j] != first) { ok = false; break; }
                if (ok)
                {
                    for (int i = 0; i < scale; i++) res.Add(new Vector2Int(i, j));
                    return res;
                }
            }
        }
        // 主对角
        first = matrix[0, 0];
        if (first != 0)
        {
            bool ok = true;
            for (int i = 1; i < scale; i++)
                if (matrix[i, i] != first) { ok = false; break; }
            if (ok)
            {
                for (int i = 0; i < scale; i++) res.Add(new Vector2Int(i, i));
                return res;
            }
        }
        // 副对角
        first = matrix[0, scale - 1];
        if (first != 0)
        {
            bool ok = true;
            for (int i = 1; i < scale; i++)
                if (matrix[i, scale - 1 - i] != first) { ok = false; break; }
            if (ok)
            {
                for (int i = 0; i < scale; i++) res.Add(new Vector2Int(i, scale - 1 - i));
                return res;
            }
        }
        return null;
    }

    int win()
    {
        int first;
        // 横
        for (int i = 0; i < scale; ++i)
        {
            first = matrix[i, 0];
            if (first != 0)
                for (int j = 1; j < scale; ++j)
                {
                    if (matrix[i, j] != first)
                        break;
                    if (j == scale - 1)
                        return first;
                }
        }
        // 纵
        for (int j = 0; j < scale; ++j)
        {
            first = matrix[0, j];
            if (first != 0)
                for (int i = 1; i < scale; ++i)
                {
                    if (matrix[i, j] != first)
                        break;
                    if (i == scale - 1)
                        return first;
                }
        }
        // 对角线
        first = matrix[0, 0];
        if (first != 0)
            for (int i = 1; i < scale; ++i)
            {
                if (matrix[i, i] != first)
                    break;
                if (i == scale - 1)
                    return first;
            }
        first = matrix[0, scale - 1];
        if (first != 0)
            for (int i = 1; i < scale; ++i)
            {
                if (matrix[i, scale - 1 - i] != first)
                    break;
                if (i == scale - 1)
                    return first;
            }

        // 平局判断
        for (int i = 0; i < scale; ++i)
            for (int j = 0; j < scale; ++j)
                if (matrix[i, j] == 0)
                    return 0;

        return 3; // 平局
    }

    void AIMove()
    {
        if (win() != 0) return;

        int bestScore = int.MinValue;
        Vector2Int bestMove = new Vector2Int(-1, -1);

        for (int i = 0; i < scale; i++)
        {
            for (int j = 0; j < scale; j++)
            {
                if (matrix[i, j] == 0)
                {
                    matrix[i, j] = 2;
                    int score = Minimax(1, false, int.MinValue, int.MaxValue);
                    matrix[i, j] = 0;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = new Vector2Int(i, j);
                    }
                }
            }
        }

        if (bestMove.x != -1)
        {
            matrix[bestMove.x, bestMove.y] = 2;
            moveHistory.Push(new Move(bestMove.x, bestMove.y, 2));
        }
    }

    int Minimax(int depth, bool isMaximizing, int alpha, int beta)
    {
        int result = win();
        if (result == 2) return 10 - depth;
        if (result == 1) return depth - 10;
        if (result == 3) return 0;

        if (depth >= (int)aiMaxDepth) return 0;

        if (isMaximizing)
        {
            int best = int.MinValue;
            for (int i = 0; i < scale; i++)
            {
                for (int j = 0; j < scale; j++)
                {
                    if (matrix[i, j] == 0)
                    {
                        matrix[i, j] = 2;
                        int score = Minimax(depth + 1, false, alpha, beta);
                        matrix[i, j] = 0;
                        if (score > best) best = score;
                        if (best > alpha) alpha = best;
                        if (beta <= alpha) return best;
                    }
                }
            }
            return best;
        }
        else
        {
            int best = int.MaxValue;
            for (int i = 0; i < scale; i++)
            {
                for (int j = 0; j < scale; j++)
                {
                    if (matrix[i, j] == 0)
                    {
                        matrix[i, j] = 1;
                        int score = Minimax(depth + 1, true, alpha, beta);
                        matrix[i, j] = 0;
                        if (score < best) best = score;
                        if (best < beta) beta = best;
                        if (beta <= alpha) return best;
                    }
                }
            }
            return best;
        }
    }

}
