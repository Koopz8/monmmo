namespace PokeMmo.Core.Sound;

/// <summary>Where a note is in its life.</summary>
public enum EnvelopeStage
{
    /// <summary>Getting louder, from nothing.</summary>
    Attack,

    /// <summary>Falling back from the peak towards the level it will hold.</summary>
    Decay,

    /// <summary>Holding, for as long as the note is held.</summary>
    Sustain,

    /// <summary>Falling away after the note has been let go.</summary>
    Release,

    /// <summary>Finished. Nothing more comes out of this.</summary>
    Done,
}

/// <summary>
/// How loud a note is, moment to moment.
/// <para>
/// <b>Modelled, and this is the first thing in the sound work that is.</b> The four numbers
/// this takes — attack, decay, sustain and release — are <em>read</em>: they are bytes eight
/// to eleven of an instrument entry on the cartridge. What the hardware's sound driver
/// <em>does</em> with them is code, and this project does not read code. So the arithmetic
/// below is a model of that code's behaviour, and where it differs the difference is
/// audible rather than wrong-in-a-way-that-corrupts-anything.
/// </para>
/// <para>
/// The shape is the one the driver uses and is worth stating plainly: attack and release are
/// multiplied towards their target once per engine step, decay is multiplied, and sustain is
/// a level rather than a rate. Multiplication rather than addition is why a note fades
/// smoothly at every loudness instead of stepping.
/// </para>
/// </summary>
public sealed class Envelope
{
    /// <summary>
    /// The scale the driver works its levels on. <b>Modelled.</b> Nought to this, rather
    /// than nought to one, because the driver's own arithmetic is whole numbers and rounding
    /// at each step is part of what it sounds like.
    /// </summary>
    public const int Full = 255;

    private readonly int _attack;
    private readonly int _decay;
    private readonly int _sustain;
    private readonly int _release;

    private int _level;

    /// <param name="attack">Bytes eight to eleven of the instrument entry, in order. Read.</param>
    public Envelope(byte attack, byte decay, byte sustain, byte release)
    {
        // An attack of nought would never reach anything. The driver treats it as immediate,
        // which is the only reading that makes an instrument with a zero there audible at
        // all.
        _attack = attack == 0 ? Full : attack;

        _decay = decay;
        _sustain = Math.Min((int)sustain, Full);
        _release = release;

        Stage = EnvelopeStage.Attack;
    }

    public EnvelopeStage Stage { get; private set; }

    /// <summary>How loud, right now, from nought to <see cref="Full"/>.</summary>
    public int Level => _level;

    public bool IsFinished => Stage == EnvelopeStage.Done;

    /// <summary>Let the note go. What happens next is the release.</summary>
    public void Release()
    {
        if (Stage is EnvelopeStage.Done or EnvelopeStage.Release) return;

        Stage = EnvelopeStage.Release;
    }

    /// <summary>
    /// One engine step. Returns how loud the note is after it.
    /// <para>
    /// A step rather than a sample: the driver moves its envelopes on a timer far slower
    /// than the mixing rate, and moving them per sample would make every note's attack
    /// instantaneous.
    /// </para>
    /// </summary>
    public int Step()
    {
        switch (Stage)
        {
            case EnvelopeStage.Attack:
                _level += _attack;

                if (_level >= Full)
                {
                    _level = Full;

                    // Straight past decay when there is none to do, which is what a decay of
                    // 255 means and what stops a note pausing at full for a step.
                    Stage = _decay >= Full ? EnvelopeStage.Sustain : EnvelopeStage.Decay;
                }

                break;

            case EnvelopeStage.Decay:
                _level = _level * _decay / Full;

                if (_level <= _sustain)
                {
                    _level = _sustain;
                    Stage = EnvelopeStage.Sustain;
                }

                break;

            case EnvelopeStage.Sustain:
                _level = _sustain;

                // A note held at nothing is a note that has finished, and saying so is what
                // lets the mixer give the channel back.
                if (_level == 0) Stage = EnvelopeStage.Done;

                break;

            case EnvelopeStage.Release:
            {
                int fading = _level * _release / Full;

                // A release of 255 multiplies a level by one and a released note would hang
                // for ever at full loudness, holding a channel nothing can use. Any step
                // that does not actually go down is made to go down by one, which is the
                // smallest change that guarantees every note ends.
                _level = fading < _level ? fading : _level - 1;

                // Multiplying towards nought never arrives, so there is a floor and below it
                // the note is over. Without this a released note occupies a channel for ever
                // at a level nobody can hear.
                if (_level <= 0)
                {
                    _level = 0;
                    Stage = EnvelopeStage.Done;
                }

                break;
            }
        }

        return _level;
    }
}
