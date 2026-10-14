namespace HireSkilledHelpers.Workers;

internal enum WorkerSkill { Farming, Mining, Fishing, Foraging, Combat }

/// <summary>Zero is unselected; one or two selects exactly one capstone per skill.</summary>
internal sealed class WorkerPerkChoices
{
    public int Farming { get; set; }
    public int Mining { get; set; }
    public int Fishing { get; set; }
    public int Foraging { get; set; }
    public int Combat { get; set; }
    private static int Valid(int choice) => choice is 1 or 2 ? choice : 0;
    public int Get(WorkerSkill skill) => Valid(skill switch
    {
        WorkerSkill.Farming => Farming, WorkerSkill.Mining => Mining, WorkerSkill.Fishing => Fishing,
        WorkerSkill.Foraging => Foraging, WorkerSkill.Combat => Combat, _ => 0,
    });
    public void Set(WorkerSkill skill, int choice)
    {
        switch (skill)
        {
            case WorkerSkill.Farming: Farming = Valid(choice); break;
            case WorkerSkill.Mining: Mining = Valid(choice); break;
            case WorkerSkill.Fishing: Fishing = Valid(choice); break;
            case WorkerSkill.Foraging: Foraging = Valid(choice); break;
            case WorkerSkill.Combat: Combat = Valid(choice); break;
        }
    }
    public WorkerPerkChoices Clone() => new() { Farming = Valid(Farming), Mining = Valid(Mining),
        Fishing = Valid(Fishing), Foraging = Valid(Foraging), Combat = Valid(Combat) };
}
