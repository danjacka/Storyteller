﻿using FubuTestingSupport;
using NUnit.Framework;
using Storyteller.Core.Model;

namespace Storyteller.Core.Testing.Grammars
{
    [TestFixture]
    public class FixtureLibraryTester
    {
        [Test]
        public void builds_for_all_the_non_hidden_fixtures()
        {
            var library = FixtureLibrary.CreateForAppDomain();

            library.Fixtures["Open"].ShouldBeOfType<OpenFixture>();

            // aliased fixture

            library.Fixtures["HanSolo"].ShouldBeOfType<AliasedFixture>();

            library.Fixtures.Has(new SecretFixture().Key).ShouldBeFalse();
        }
    }

    [Hidden]
    public class SecretFixture : Fixture
    {
        
    }

    public class OpenFixture : Fixture
    {
        
    }

    public class AliasedFixture : Fixture
    {
        public AliasedFixture()
        {
            Key = "HanSolo";
        }
    }
}