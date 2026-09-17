using System;

[Serializable]
public class PlayerData
{
    public float maxStamina = 100f;
    public float stamina = 100f;

    public int ore = 0;

    public float weight = 10f;
    public float maxWeight = 30f;

    public int piton = 3; // หมุดเปรียบเสมือนจำนวนชีวิต
}