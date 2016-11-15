using System;
using StoryTeller.Model;
using StoryTeller.Remotes;

namespace ST.Client
{
    public interface IFixtureController : IDisposable
    {
        void StartWatching(string path);
        void RecordSystemFixtures(SystemRecycled recycled);
        FixtureModel[] CombinedFixtures();
        void ReloadFixtures();
        void ExportAllFixtures();
        void Export(string key);
        string FileFor(string key);
        string CreateFixture(string keyOrTitle);
    }
}