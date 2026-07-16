using System.Collections.Generic;
using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Ball ball;

    [Header("Goal")]
    [SerializeField] private int scoringPlayer = 1;

    [SerializeField]
    private Vector3 gridSize = new Vector3(4f, 2.5f, 2f);
    [SerializeField, Min(0.25f)] private float pointSpacing = 0.25f;
    [SerializeField] private float pointRadius = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float requiredBallPercentage = 0.5f;

    private readonly List<Vector3> localGridPoints =
        new List<Vector3>();

    /*
     * Cantidad aproximada de puntos que habría
     * dentro de una pelota completa usando el
     * mismo espaciado que la grilla del arco.
     */
    private int fullBallPointCount;

    private bool goalRegistered;

    private void Awake()
    {
        BuildGoalGrid();
        CalculateFullBallPointCount();
    }

    private void FixedUpdate()
    {
        if (ball == null || gameManager == null)
            return;

        if (gameManager.MatchFinished ||
            gameManager.RoundLocked)
        {
            return;
        }

        int pointsInsideBall = 0;

        Vector3 ballCenter =
            ball.WorldCenter;

        float ballRadius =
            ball.Radius;

        float radiusSquared =
            ballRadius *
            ballRadius;

        for (int i = 0;
             i < localGridPoints.Count;
             i++)
        {
            Vector3 worldGridPoint =
                transform.TransformPoint(
                    localGridPoints[i]);

            float distanceSquared =
                (worldGridPoint -
                 ballCenter).sqrMagnitude;

            if (distanceSquared <= radiusSquared)
                pointsInsideBall++;
        }

        float percentageInside =
            fullBallPointCount > 0
                ? pointsInsideBall /
                  (float)fullBallPointCount
                : 0f;

        percentageInside =
            Mathf.Clamp01(
                percentageInside);

        bool isGoal =
            percentageInside >=
            requiredBallPercentage;

        if (isGoal && !goalRegistered)
        {
            goalRegistered = true;

            gameManager.RegisterGoal(
                scoringPlayer);
        }
        else if (!isGoal)
        {
            goalRegistered = false;
        }
    }

    private void BuildGoalGrid()
    {
        localGridPoints.Clear();

        pointSpacing = Mathf.Max(pointSpacing, 0.05f);

        Vector3 halfSize = gridSize * 0.5f;

        int pointsX = Mathf.FloorToInt(gridSize.x / pointSpacing);
        int pointsY = Mathf.FloorToInt(gridSize.y / pointSpacing);
        int pointsZ = Mathf.FloorToInt(gridSize.z / pointSpacing);

        for (int x = 0; x <= pointsX; x++)
        {
            float localX = -halfSize.x + x * pointSpacing;

            for (int y = 0; y <= pointsY; y++)
            {
                float localY = -halfSize.y + y * pointSpacing;

                for (int z = 0; z <= pointsZ; z++)
                {
                    float localZ = -halfSize.z + z * pointSpacing;
                    localGridPoints.Add(new Vector3(localX, localY, localZ));
                }
            }
        }
    }

    private void CalculateFullBallPointCount()
    {
        fullBallPointCount = 0;

        if (ball == null)
            return;

        float radius = ball.Radius;
        float radiusSquared = radius * radius;

        int pointRange = Mathf.CeilToInt(radius / pointSpacing);

        for (int x = -pointRange; x <= pointRange; x++)
        {
            for (int y = -pointRange; y <= pointRange; y++)
            {
                for (int z = -pointRange; z <= pointRange; z++)
                {
                    Vector3 point = new Vector3(x * pointSpacing, y * pointSpacing, z * pointSpacing);

                    if (point.sqrMagnitude <= radiusSquared)
                        fullBallPointCount++;
                }
            }
        }
    }

    private void OnValidate()
    {
        pointSpacing = Mathf.Max(pointSpacing, 0.05f);

        BuildGoalGrid();

        if (ball != null)
            CalculateFullBallPointCount();
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, gridSize);

        Gizmos.color = Color.cyan;

        if (localGridPoints.Count == 0)
            BuildGoalGrid();

        for (int i = 0; i < localGridPoints.Count; i++)
        {
            Gizmos.DrawSphere(localGridPoints[i], pointRadius);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}