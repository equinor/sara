using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Models;

[Owned]
public class Position
{
    public Position() { }

    public Position(Position copy)
    {
        X = copy.X;
        Y = copy.Y;
        Z = copy.Z;
    }

    public Position(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [Required]
    [JsonPropertyName("x")]
    public float X { get; set; }

    [Required]
    [JsonPropertyName("y")]
    public float Y { get; set; }

    [Required]
    [JsonPropertyName("z")]
    public float Z { get; set; }
}

[Owned]
public class Orientation
{
    public Orientation()
    {
        W = 1;
    }

    public Orientation(Orientation copy)
    {
        X = copy.X;
        Y = copy.Y;
        Z = copy.Z;
        W = copy.W;
    }

    public Orientation(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    [Required]
    [JsonPropertyName("x")]
    public float X { get; set; }

    [Required]
    [JsonPropertyName("y")]
    public float Y { get; set; }

    [Required]
    [JsonPropertyName("z")]
    public float Z { get; set; }

    [Required]
    [JsonPropertyName("w")]
    public float W { get; set; }
}

[Owned]
public class Pose
{
    public Pose()
    {
        Position = new Position();
        Orientation = new Orientation();
    }

    public Pose(Pose copy)
    {
        Position = new Position(copy.Position);
        Orientation = new Orientation(copy.Orientation);
    }

    public Pose(Position position, Orientation orientation)
    {
        Position = position;
        Orientation = orientation;
    }

    [Required]
    [JsonPropertyName("position")]
    public Position Position { get; set; }

    [Required]
    [JsonPropertyName("orientation")]
    public Orientation Orientation { get; set; }
}
