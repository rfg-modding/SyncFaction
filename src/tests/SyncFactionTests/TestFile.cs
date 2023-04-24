using System.IO.Abstractions.TestingHelpers;

namespace SyncFactionTests;

public class TestFile
{
    private readonly MockFileSystem fs;

    private MockFileData fileData = new MockFileData(string.Empty);

    private string name = string.Empty;
    private string fullPath = string.Empty;

    public TestFile(MockFileSystem fs)
    {
        this.fs = fs;
    }

    public TestFile Data(string data)
    {
        fileData = new MockFileData(data);
        return this;
    }

    public TestFile Name(string name)
    {
        this.name = name;
        return this;
    }

    public void Delete()
    {
        fs.RemoveFile(fullPath);
    }

    public TestFile Make(File file, Data data)
    {
        this.name = file switch
        {
            File.Exe => Fs.Names.Exe,
            File.Dll => Fs.Names.Dll,
            File.Vpp => Fs.Names.Vpp,
            File.Txt => Fs.Names.Txt,
            File.Etc => Fs.Names.Etc,
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
        };

        this.fileData = data switch
        {
            SyncFactionTests.Data.None => string.Empty,
            SyncFactionTests.Data.Orig => file switch
            {
                File.Exe => Fs.Contents.Orig.Exe,
                File.Dll => Fs.Contents.Orig.Dll,
                File.Vpp => Fs.Contents.Orig.Vpp,
                File.Txt => Fs.Contents.Orig.Txt,
                File.Etc => Fs.Contents.Orig.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Drty => file switch
            {
                File.Exe => Fs.Contents.Drty.Exe,
                File.Dll => Fs.Contents.Drty.Dll,
                File.Vpp => Fs.Contents.Drty.Vpp,
                File.Txt => Fs.Contents.Drty.Txt,
                File.Etc => Fs.Contents.Drty.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Mod1 => file switch
            {
                File.Exe => Fs.Contents.Mod1.Exe,
                File.Dll => Fs.Contents.Mod1.Dll,
                File.Vpp => Fs.Contents.Mod1.Vpp,
                File.Txt => Fs.Contents.Mod1.Txt,
                File.Etc => Fs.Contents.Mod1.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Mod2 => file switch
            {
                File.Exe => Fs.Contents.Mod2.Exe,
                File.Dll => Fs.Contents.Mod2.Dll,
                File.Vpp => Fs.Contents.Mod2.Vpp,
                File.Txt => Fs.Contents.Mod2.Txt,
                File.Etc => Fs.Contents.Mod2.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Pch1 => file switch
            {
                File.Exe => Fs.Contents.Pch1.Exe,
                File.Dll => Fs.Contents.Pch1.Dll,
                File.Vpp => Fs.Contents.Pch1.Vpp,
                File.Txt => Fs.Contents.Pch1.Txt,
                File.Etc => Fs.Contents.Pch1.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Pch2 => file switch
            {
                File.Exe => Fs.Contents.Pch2.Exe,
                File.Dll => Fs.Contents.Pch2.Dll,
                File.Vpp => Fs.Contents.Pch2.Vpp,
                File.Txt => Fs.Contents.Pch2.Txt,
                File.Etc => Fs.Contents.Pch2.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(data), data, null)
        };

        return this;
    }

    public TestFile In(string absPath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Place file after initializing name and data");
        }
        fullPath = fs.Path.Combine(absPath, name);
        fs.AddFile(fullPath, fileData);
        return this;
    }
}