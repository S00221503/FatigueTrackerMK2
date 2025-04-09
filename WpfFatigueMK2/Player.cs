using System;

public class Player
{
    public int Id { get; set; }         // Unique identifier
    public string Name { get; set; }    // Display name
    public string Position { get; set; } // Optional: Add this if your DB has it
    public int TeamId { get; set; }     // Optional: if you're tracking teams
    public int AverageSteps { get; set; } // Optional: useful for other logic

    public override string ToString()
    {
        return Name; // Makes the player name appear in the ListBox
    }
}
