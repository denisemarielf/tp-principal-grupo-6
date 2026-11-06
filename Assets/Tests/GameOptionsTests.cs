using System;
using System.IO;
using NUnit.Framework;

public class GameOptionsTests
{
    private string path;

    [SetUp]
    public void SetUp()
    {
        path = Path.Combine(Path.GetTempPath(), "GameOptionsTests_" + Guid.NewGuid().ToString("N") + ".json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);

        string temp = path + ".tmp";
        if (File.Exists(temp))
            File.Delete(temp);
    }

    [Test]
    public void GuardarYCargar_ConservaVolumenSensibilidadYCalidad()
    {
        var data = new GameOptionsData
        {
            master = 0.4f,
            music = 0.25f,
            sfx = 0.8f,
            sensitivity = 3.5f,
            qualityLevel = 1,
            fullScreen = false,
            width = 1280,
            height = 720,
            vSync = 0,
            targetFrameRate = 60
        };

        Assert.IsTrue(GameOptions.TryWrite(data, path));

        GameOptionsData loaded = GameOptions.TryRead(path);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(0.4f, loaded.master, 0.001f);
        Assert.AreEqual(0.25f, loaded.music, 0.001f);
        Assert.AreEqual(0.8f, loaded.sfx, 0.001f);
        Assert.AreEqual(3.5f, loaded.sensitivity, 0.001f);
        Assert.AreEqual(1, loaded.qualityLevel);
        Assert.IsFalse(loaded.fullScreen);
        Assert.AreEqual(1280, loaded.width);
        Assert.AreEqual(720, loaded.height);
        Assert.AreEqual(0, loaded.vSync);
        Assert.AreEqual(60, loaded.targetFrameRate);
    }
}
