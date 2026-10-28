using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The poof's sound: a landing is silent, Clear's smoke is one whoosh per burst.</summary>
    public class PoofSoundPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();
        AudioClip m_Smoke;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        static AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 4800, 1, 48000, false);
            var data = new float[4800];
            for (int i = 0; i < data.Length; i++) data[i] = 0.3f * Mathf.Sin(i * 0.05f) * (1f - i / 4800f);
            clip.SetData(data, 0);
            return clip;
        }

        PoofCloud[] Build(int count)
        {
            m_Smoke ??= Clip("TestSmoke");
            var clouds = new PoofCloud[count];
            for (int c = 0; c < count; c++)
            {
                var go = new GameObject($"PoofCloud{c}");
                m_Spawned.Add(go);
                var puffs = new Transform[3];
                for (int i = 0; i < puffs.Length; i++)
                {
                    puffs[i] = new GameObject($"Puff{i}").transform;
                    puffs[i].SetParent(go.transform, false);
                }
                clouds[c] = go.AddComponent<PoofCloud>();
                clouds[c].Configure(puffs, null);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                clouds[c].ConfigureSound(source, m_Smoke);
            }
            return clouds;
        }

        [UnityTest]
        public IEnumerator ALandingIsSilentAndClearsSmokeIsOneWhoosh()
        {
            var clouds = Build(4);
            yield return new WaitForSeconds(PoofCloud.SoundGapSeconds); // clear of any earlier test's poof
            clouds[0].Play(Vector3.zero);
            Assert.IsNull(clouds[0].LastSound, "a landing poofs without a sound");
            Assert.IsFalse(clouds[0].GetComponent<AudioSource>().isPlaying);

            yield return new WaitForSeconds(PoofCloud.Seconds + 0.05f);
            for (int c = 1; c < clouds.Length; c++) clouds[c].Play(Vector3.right * c, JungleDirector.ClearSmokeSize);
            Assert.AreSame(m_Smoke, clouds[1].LastSound, "Clear's big smoke whooshes");
            Assert.IsNull(clouds[2].LastSound, "clouds bursting together share the one sound");
            Assert.IsNull(clouds[3].LastSound);
            Assert.IsFalse(clouds[3].GetComponent<AudioSource>().isPlaying, "never a stack of whooshes");
            float firstPitch = clouds[1].GetComponent<AudioSource>().pitch;

            yield return new WaitForSeconds(PoofCloud.Seconds + 0.05f);
            clouds[1].Play(Vector3.zero, JungleDirector.ClearSmokeSize);
            Assert.AreSame(m_Smoke, clouds[1].LastSound, "the next Clear whooshes again");
            Assert.AreNotEqual(firstPitch, clouds[1].GetComponent<AudioSource>().pitch, "a slightly different whoosh each time");
        }
    }
}
