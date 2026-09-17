using FMOD;
using Thread = System.Threading.Thread;

namespace Real;

public class SoundTest
{
    public static void Test()
    {
        Check(Factory.System_Create(out FMOD.System system));
        Check(system.init(32, INITFLAGS.NORMAL, IntPtr.Zero));

        Check(system.createSound("yay.mp3", MODE.DEFAULT | MODE.CREATESTREAM | MODE.LOOP_OFF, out Sound sound));

        Check(system.playSound(sound, new ChannelGroup(), false, out _));

        bool isPlaying = true;
        while (isPlaying)
        {
            Check(system.update());

            Check(system.getChannelsPlaying(out int channelsPlaying));
            if (channelsPlaying == 0)
            {
                isPlaying = false;
            }

            Thread.Sleep(10);
        }

        Check(sound.release());
        Check(system.close());
        Check(system.release());
    }

    private static void Check(RESULT result)
    {
        if (result != RESULT.OK)
            throw new InvalidOperationException($"Ошибка FMOD: {result}");
    }
}