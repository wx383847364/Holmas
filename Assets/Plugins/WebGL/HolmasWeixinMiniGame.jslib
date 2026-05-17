mergeInto(LibraryManager.library, {
  $HolmasWeixinMiniGameState: {
    windowInfoBuffer: 0
  },

  HolmasWeixinMiniGame_IsAvailable: function () {
    return typeof wx !== "undefined" ? 1 : 0;
  },

  HolmasWeixinMiniGame_Login: function (callbackObjectNamePtr, successMethodNamePtr, failMethodNamePtr) {
    var callbackObjectName = UTF8ToString(callbackObjectNamePtr);
    var successMethodName = UTF8ToString(successMethodNamePtr);
    var failMethodName = UTF8ToString(failMethodNamePtr);

    function fail(message) {
      var sendMessage = typeof SendMessage === "function"
        ? SendMessage
        : (typeof Module !== "undefined" && typeof Module.SendMessage === "function" ? Module.SendMessage.bind(Module) : null);
      if (sendMessage) {
        sendMessage(callbackObjectName, failMethodName, message || "");
      }
    }

    if (typeof wx === "undefined" || typeof wx.login !== "function") {
      fail("wx.login unavailable");
      return;
    }

    wx.login({
      success: function (res) {
        var code = res && res.code ? res.code : "";
        var sendMessage = typeof SendMessage === "function"
          ? SendMessage
          : (typeof Module !== "undefined" && typeof Module.SendMessage === "function" ? Module.SendMessage.bind(Module) : null);
        if (sendMessage) {
          sendMessage(callbackObjectName, successMethodName, code);
        }
      },
      fail: function (err) {
        fail(JSON.stringify(err || {}));
      }
    });
  },

  HolmasWeixinMiniGame_GetWindowInfoJson: function () {
    var info = null;
    try {
      if (typeof wx !== "undefined" && typeof wx.getWindowInfo === "function") {
        info = wx.getWindowInfo();
      } else if (typeof wx !== "undefined" && typeof wx.getSystemInfoSync === "function") {
        info = wx.getSystemInfoSync();
      }
    } catch (err) {
      info = { error: String(err || "") };
    }

    var json = JSON.stringify(info || {});
    var length = lengthBytesUTF8(json) + 1;
    if (HolmasWeixinMiniGameState.windowInfoBuffer) {
      _free(HolmasWeixinMiniGameState.windowInfoBuffer);
    }

    HolmasWeixinMiniGameState.windowInfoBuffer = _malloc(length);
    stringToUTF8(json, HolmasWeixinMiniGameState.windowInfoBuffer, length);
    return HolmasWeixinMiniGameState.windowInfoBuffer;
  }
});
