namespace MoonscarsTASTools.uTas.Communication;

// every String is UTF-8, every integer is big endian unless spcified otherwise

public enum ClientOpCode : byte {
    EstablishConnection = 0,
    CloseConnection = 1,
    SetInfoString = 2, // String
    SetStudioInfo = 3, // StudioInfo
    SendKeybindings = 4
}

public enum ServerOpCode : byte {
    KeybindTriggered = 0,
    SendPath = 1 // String
}

public enum TasKeybind : byte {
    StartStop = 0,
    FrameAdvance = 1,
    PauseResume = 2,

    ToggleHitboxes = 3
}

// ReSharper disable once UnusedMember.Global
public readonly record struct StudioInfo(
    int CurrentLine, // 1-indexed
    string CurrentLineSuffix,
    int CurrentFrameInTas,
    int TotalFrames,
    int SaveStateLine,
    int TasStates,
    string LevelName,
    string ChapterTime
) {
    public static readonly StudioInfo Invalid = new(-1, "", 0, 0, 0, 0, "", "");

    public byte[] ToByteArray() {
        return BinaryFormatterHelper.ToByteArray(new object[] {
            CurrentLine,
            CurrentLineSuffix,
            CurrentFrameInTas,
            TotalFrames,
            SaveStateLine,
            TasStates,
            LevelName,
            ChapterTime
        });
    }

    public static StudioInfo FromByteArray(byte[] data) {
        var values = BinaryFormatterHelper.FromByteArray<object[]>(data);
        return new StudioInfo(
            (int)values[0],
            (values[1] as string)!,
            (int)values[2],
            (int)values[3],
            (int)values[4],
            (int)values[5],
            (values[6] as string)!,
            (values[7] as string)!
        );
    }
}