using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Karawara.Core.Session;
using NUnit.Framework;
using RPGKarawara;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Karawara.Tests.Gameplay
{
    public abstract class SessionFixture
    {
        protected GameBootstrap bootstrap;
        private string previousRoot;
        [UnitySetUp] public IEnumerator PrepareMenu()
        {
            yield return SceneManager.LoadSceneAsync("SceneMenu"); yield return null;
            bootstrap = GameBootstrap.EnsureInstance();
            previousRoot = WorldSaveGameManager.instance.SaveRootOverride;
            WorldSaveGameManager.instance.SaveRootOverride = Path.Combine(Path.GetTempPath(), "Karawara-G3A-Tests-" + Guid.NewGuid().ToString("N"));
        }
        [UnityTearDown] public IEnumerator ReleaseSession()
        {
            var operation = bootstrap.Session.ReturnToMenuAsync();
            while (!operation.IsCompleted) yield return null;
            Assert.That(operation.IsFaulted, Is.False);
            WorldSaveGameManager.instance.SaveRootOverride = previousRoot;
            yield return null;
        }
        protected static IEnumerator Await(Task<SessionResult> operation)
        {
            float end = Time.realtimeSinceStartup + 40;
            while (!operation.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(operation.IsCompleted, Is.True, "Scene/session operation exceeded its bounded wait");
            Assert.That(operation.IsFaulted, Is.False);
            Assert.That(operation.Result, Is.EqualTo(SessionResult.Success));
        }
    }
    public class SessionLifecycleTests : SessionFixture
    {
        [UnityTest] public IEnumerator MenuGameMenuReleasesContextAndRetainsCatalog()
        {
            var catalog = bootstrap.Catalog;
            Assert.That(catalog != null, Is.True);
            yield return Await(bootstrap.Session.StartNewGameAsync(1));
            var context = bootstrap.Session.Player.Context;
            Assert.That(context.IsDisposed, Is.False);
            Assert.That(context.Catalog, Is.SameAs(catalog));
            yield return Await(bootstrap.Session.ReturnToMenuAsync()); yield return null;
            Assert.That(context.IsDisposed, Is.True);
            Assert.That(context.UI == null && context.Input == null, Is.True);
            Assert.That(bootstrap.Catalog, Is.SameAs(catalog));
            Assert.That(bootstrap.Session.HasActiveSession, Is.False);
        }
    }
}
