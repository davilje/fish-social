#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Social;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopProtocolResponseTests
    {
        [Serializable]
        sealed class CoinsResponse
        {
            public int coins;
        }

        [Serializable]
        sealed class InventoryResponse
        {
            public string[] items;
        }

        [Test]
        public void AuthResponseAcceptsCompletePayload()
        {
            string error;
            object parsed;
            Assert.IsTrue(InvokeParser(
                typeof(AuthenticatedApiClient),
                typeof(CoinsResponse),
                "{\"coins\":12}",
                new[] { "coins" },
                out parsed,
                out error));
            Assert.IsNull(error);
            Assert.AreEqual(12, ((CoinsResponse)parsed).coins);
        }

        [Test]
        public void AuthResponseAcceptsExplicitEmptyArray()
        {
            string error;
            object parsed;
            Assert.IsTrue(InvokeParser(
                typeof(AuthenticatedApiClient),
                typeof(InventoryResponse),
                "{\"items\":[]}",
                new[] { "items" },
                out parsed,
                out error));
            Assert.IsNull(error);
            Assert.AreEqual(0, ((InventoryResponse)parsed).items.Length);
        }

        [Test]
        public void AuthResponseRejectsMissingField()
        {
            string error;
            object parsed;
            Assert.IsFalse(InvokeParser(
                typeof(AuthenticatedApiClient),
                typeof(CoinsResponse),
                "{}",
                new[] { "coins" },
                out parsed,
                out error));
            Assert.IsNull(parsed);
            Assert.AreEqual("服务端响应格式错误，请重试。", error);
        }

        [Test]
        public void LobbyResponseRejectsEmptyAndMalformedPayloads()
        {
            string error;
            object parsed;
            Assert.IsFalse(InvokeParser(
                typeof(SocialLobbyApiClient),
                typeof(CoinsResponse),
                string.Empty,
                new[] { "lobby" },
                out parsed,
                out error));
            Assert.AreEqual("服务端响应格式错误，请重试。", error);

            Assert.IsFalse(InvokeParser(
                typeof(SocialLobbyApiClient),
                typeof(CoinsResponse),
                "{\"lobby\":",
                new[] { "lobby" },
                out parsed,
                out error));
            Assert.AreEqual("服务端响应格式错误，请重试。", error);
        }

        static bool InvokeParser(
            Type owner,
            Type responseType,
            string json,
            string[] fields,
            out object parsed,
            out string error)
        {
            var method = owner.GetMethod(
                "TryParseResponse",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var generic = method.MakeGenericMethod(responseType);
            var arguments = new object[] { json, null, null, fields };
            var result = (bool)generic.Invoke(null, arguments);
            parsed = arguments[1];
            error = arguments[2] as string;
            return result;
        }
    }
}
#endif
