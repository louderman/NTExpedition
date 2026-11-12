// Lab 5 actor tier. Names match the State assets one-to-one so a state asset name can be parsed
// straight back into the enum (see TransformDecision / EnumExtension).
public enum ExpeditionerState
{
    Default = -1,
    Base = 0,
    Armored = 1,
    Armed = 2,
    Recovering = 3,
    Downed = 99
}

// What a collected powerup asks the actor state machine to do.
// Note that Damage is a trigger too: damage is not an instant effect, it is a request that the
// current state's transitions decide how to handle (exactly like the handout's PowerupType.Damage).
public enum PowerupType
{
    Default = -1,
    SuitKit = 0,
    KineticCore = 1,
    Repair = 2,
    SuitOvercharge = 3,
    Shield = 4,
    Damage = 99
}

// What a collected buff asks the *buff* state machine to do (separate controller, separate FSM).
public enum BuffType
{
    Default = -1,
    Overcharged = 0,
    Invincible = 1
}

// Names match the buff State assets: BuffDefault / BuffOvercharged / BuffInvincible.
public enum BuffState
{
    BuffDefault = 0,
    BuffOvercharged = 1,
    BuffInvincible = 2
}
