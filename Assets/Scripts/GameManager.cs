using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;

    [Header("Ball")]
    [SerializeField] private Ball ball;

    [Header("Spawn points")]
    [SerializeField] private Transform car1Spawn;
    [SerializeField] private Transform car2Spawn;
    [SerializeField] private Transform ballSpawn;

    [Header("Match")]
    [SerializeField] private int goalsToWin = 3;
    [SerializeField] private float resetDelay = 1f;

    private int player1Score;
    private int player2Score;

    private bool roundLocked;
    private bool matchFinished;

    public int Player1Score => player1Score;
    public int Player2Score => player2Score;

    public bool RoundLocked => roundLocked;
    public bool MatchFinished => matchFinished;

    public void RegisterGoal(int scoringPlayer)
    {
        if (roundLocked || matchFinished)
            return;

        roundLocked = true;

        if (scoringPlayer == 1)
        {
            player1Score++;
        }
        else if (scoringPlayer == 2)
        {
            player2Score++;
        }
        else
        {
            Debug.LogError($"Jugador inválido: {scoringPlayer}");

            roundLocked = false;
            return;
        }

        Debug.Log($"Gol del jugador {scoringPlayer} | " + $"{player1Score} - {player2Score}");

        if (player1Score >= goalsToWin)
        {
            FinishMatch(1);
            return;
        }

        if (player2Score >= goalsToWin)
        {
            FinishMatch(2);
            return;
        }

        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSeconds(resetDelay);

        ResetRound();

        roundLocked = false;
    }

    private void ResetRound()
    {
        if (car1 != null && car1Spawn != null)
            car1.ResetCar(car1Spawn.position, car1Spawn.rotation);

        if (car2 != null && car2Spawn != null)
            car2.ResetCar(car2Spawn.position, car2Spawn.rotation);

        if (ball != null && ballSpawn != null)
            ball.ResetBall(ballSpawn.position, ballSpawn.rotation);
    }

    private void FinishMatch(int winner)
    {
        matchFinished = true;

        if (car1 != null)
        {
            car1.SetMovementInput(0f);
            car1.SetRotationInput(0f);
        }

        if (car2 != null)
        {
            car2.SetMovementInput(0f);
            car2.SetRotationInput(0f);
        }

        Debug.Log(
            $"Jugador {winner} gana la partida " +
            $"{player1Score} - {player2Score}");
    }
}