using UnityEngine;

[System.Serializable]
public struct LidarRay
{
    public float angle;
    public float distance;
    public Vector3 hitPoint;
    public bool hitObstacle;
    
    public LidarRay(float angle, float distance, Vector3 hitPoint, bool hitObstacle)
    {
        this.angle = angle;
        this.distance = distance;
        this.hitPoint = hitPoint;
        this.hitObstacle = hitObstacle;
    }

    public override string ToString()
    {
        return $"Angle: {angle}°, Distance: {distance:F2}, Hit: {hitObstacle}";
    }
}