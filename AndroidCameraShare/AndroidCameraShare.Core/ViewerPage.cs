namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Страницы зрителя. Живут в Core, чтобы телефон и тесты отдавали один HTML.
    /// </summary>
    public static class ViewerPage
    {
        public const string PinFormHtml =
            """
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <link rel="icon" href="data:,">
              <title>Няня</title>
            </head>
            <body>
              <p>Введите PIN</p>
              <input id="pin" type="password" inputmode="numeric" maxlength="4" autocomplete="off">
              <button id="enter" type="button">Открыть</button>
              <script>
                document.getElementById('enter').onclick = function () {
                  var pin = document.getElementById('pin').value;
                  if (pin.length !== 4) {
                    return;
                  }
                  document.cookie = 'pin=' + encodeURIComponent(pin) + '; Path=/; SameSite=Strict';
                  location.reload();
                };
              </script>
            </body>
            </html>
            """;

        public const string WatchHtml =
            """
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
              <link rel="icon" href="data:,">
              <title>Няня</title>
              <style>
                html, body { height: 100%; }
                body {
                  font-family: sans-serif;
                  margin: 0;
                  padding: 0;
                  background: #111;
                  color: #eee;
                  display: flex;
                  flex-direction: column;
                  box-sizing: border-box;
                  height: 100dvh;
                  overflow: hidden;
                }
                .stage {
                  flex: 1;
                  min-height: 0;
                  position: relative;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  background: #000;
                  overflow: hidden;
                }
                video {
                  width: 100%;
                  height: 100%;
                  object-fit: contain;
                  background: #000;
                }
                video.landscape-rotate {
                  position: absolute;
                  left: 50%;
                  top: 50%;
                  object-fit: contain;
                }
                .hud {
                  position: absolute;
                  left: 0;
                  right: 0;
                  top: 0;
                  z-index: 2;
                  display: flex;
                  gap: 12px;
                  align-items: baseline;
                  flex-wrap: wrap;
                  padding: calc(8px + env(safe-area-inset-top, 0px)) 12px 20px;
                  background: linear-gradient(rgba(0,0,0,.72), transparent);
                  pointer-events: none;
                }
                #status, #battery {
                  margin: 0;
                  font-size: 14px;
                  text-shadow: 0 1px 2px #000;
                }
                #battery { color: #ccc; }
                .error { color: #f66; }
                .actions {
                  position: absolute;
                  left: 0;
                  right: 0;
                  bottom: 0;
                  z-index: 2;
                  display: flex;
                  gap: 14px;
                  flex-wrap: wrap;
                  align-items: center;
                  justify-content: center;
                  padding: 16px 12px calc(14px + env(safe-area-inset-bottom, 0px));
                  background: linear-gradient(transparent, rgba(0,0,0,.78));
                }
                .icon-btn {
                  width: 52px;
                  height: 52px;
                  padding: 0;
                  border: 0;
                  border-radius: 50%;
                  background: rgba(255,255,255,.16);
                  color: #fff;
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                  cursor: pointer;
                  backdrop-filter: blur(8px);
                }
                .icon-btn:hover { background: rgba(255,255,255,.28); }
                .icon-btn:active { transform: scale(.96); }
                .icon-btn svg { width: 22px; height: 22px; fill: currentColor; display: block; }
                .icon-btn.stop svg { width: 16px; height: 16px; }
                .icon-btn[hidden] { display: none; }
                @media (hover: none) and (pointer: coarse) {
                  .actions {
                    opacity: 0;
                    pointer-events: none;
                    transition: opacity .2s;
                  }
                  body.controls-on .actions {
                    opacity: 1;
                    pointer-events: auto;
                  }
                }
              </style>
            </head>
            <body>
              <div class="stage" id="stage">
                <video id="video" autoplay playsinline></video>
                <div class="hud">
                  <p id="status"></p>
                  <p id="battery"></p>
                </div>
                <div class="actions">
                  <button id="stop" class="icon-btn stop" type="button" aria-label="Остановить просмотр" title="Стоп">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>
                  </button>
                  <button id="watch" class="icon-btn" type="button" hidden aria-label="Смотреть" title="Смотреть">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 5v14l11-7z"/></svg>
                  </button>
                  <button id="flip" class="icon-btn" type="button" aria-label="Повернуть 180°" title="Повернуть 180°">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5V2L8 6l4 4V7c2.76 0 5 2.24 5 5s-2.24 5-5 5-5-2.24-5-5H5c0 3.87 3.13 7 7 7s7-3.13 7-7-3.13-7-7-7z"/></svg>
                  </button>
                  <button id="camera" class="icon-btn" type="button" aria-label="Сменить камеру" title="Сменить камеру">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 3 7.17 5H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-3.17L15 3H9zm3 15a5 5 0 1 1 0-10 5 5 0 0 1 0 10zm0-2.2a2.8 2.8 0 1 0 0-5.6 2.8 2.8 0 0 0 0 5.6z"/></svg>
                  </button>
                </div>
              </div>
              <script>
                (function () {
                  var video = document.getElementById('video');
                  var stage = document.getElementById('stage');
                  var status = document.getElementById('status');
                  var battery = document.getElementById('battery');
                  var stopBtn = document.getElementById('stop');
                  var watchBtn = document.getElementById('watch');
                  var flipBtn = document.getElementById('flip');
                  var cameraBtn = document.getElementById('camera');
                  var pc = null;
                  var reconnectTimer = null;
                  var batteryTimer = null;
                  var generation = 0;
                  var stopped = false;
                  var cameraFacing = 'back';
                  var userFlip = false;

                  function isTouchUi() {
                    return window.matchMedia('(hover: none) and (pointer: coarse)').matches;
                  }

                  function rotationOffset() {
                    var base = cameraFacing === 'back' ? 180 : 0;
                    return userFlip ? (base + 180) % 360 : base;
                  }

                  function syncControls(isWatching) {
                    if (!isWatching || !isTouchUi()) {
                      document.body.classList.add('controls-on');
                      return;
                    }
                    document.body.classList.remove('controls-on');
                  }

                  function layoutVideo() {
                    if (!stage) {
                      return;
                    }
                    var portrait = video.videoHeight > video.videoWidth;
                    var rotate = cameraFacing === 'back' && portrait && video.videoHeight > 0;
                    var offset = rotationOffset();
                    video.classList.toggle('landscape-rotate', rotate);
                    if (!rotate) {
                      video.style.width = '100%';
                      video.style.height = '100%';
                      video.style.transform = offset ? ('rotate(' + offset + 'deg)') : '';
                      return;
                    }
                    var sw = stage.clientWidth;
                    var sh = stage.clientHeight;
                    video.style.width = sh + 'px';
                    video.style.height = sw + 'px';
                    video.style.transform = 'translate(-50%, -50%) rotate(' + (90 + offset) + 'deg)';
                  }

                  function cookiePin() {
                    var match = document.cookie.match(/(?:^|; )pin=([^;]*)/);
                    return match ? decodeURIComponent(match[1]) : '';
                  }

                  function pinHeaders() {
                    return { 'X-Pin': cookiePin() };
                  }

                  function setStatus(text, isError) {
                    status.textContent = text;
                    status.className = isError ? 'error' : '';
                  }

                  function setWatchingUi(isWatching) {
                    stopBtn.hidden = !isWatching;
                    watchBtn.hidden = isWatching;
                    syncControls(isWatching);
                  }

                  function waitIceComplete(peer, timeoutMs) {
                    return new Promise(function (resolve) {
                      var finished = false;
                      function finish() {
                        if (finished) {
                          return;
                        }
                        finished = true;
                        peer.onicegatheringstatechange = null;
                        resolve();
                      }
                      peer.onicegatheringstatechange = function () {
                        if (peer.iceGatheringState === 'complete') {
                          finish();
                        }
                      };
                      if (peer.iceGatheringState === 'complete') {
                        finish();
                        return;
                      }
                      setTimeout(finish, timeoutMs);
                    });
                  }

                  function closePeer() {
                    if (pc) {
                      pc.onicecandidate = null;
                      pc.ontrack = null;
                      pc.onicegatheringstatechange = null;
                      pc.oniceconnectionstatechange = null;
                      pc.close();
                      pc = null;
                    }
                    video.srcObject = null;
                  }

                  function clearReconnect() {
                    if (reconnectTimer) {
                      clearTimeout(reconnectTimer);
                      reconnectTimer = null;
                    }
                  }

                  function scheduleReconnect(message) {
                    if (stopped) {
                      return;
                    }
                    setStatus(message, true);
                    closePeer();
                    if (reconnectTimer) {
                      return;
                    }
                    reconnectTimer = setTimeout(function () {
                      reconnectTimer = null;
                      connect();
                    }, 2000);
                  }

                  async function hangup() {
                    var pin = cookiePin();
                    if (pin.length !== 4) {
                      return;
                    }
                    try {
                      await fetch('/hangup', {
                        method: 'POST',
                        headers: pinHeaders(),
                        keepalive: true
                      });
                    } catch (error) {
                    }
                  }

                  async function stopWatch() {
                    stopped = true;
                    generation += 1;
                    clearReconnect();
                    closePeer();
                    setWatchingUi(false);
                    setStatus('Просмотр остановлен', false);
                    await hangup();
                  }

                  async function refreshBattery() {
                    var pin = cookiePin();
                    if (pin.length !== 4) {
                      return;
                    }
                    try {
                      var response = await fetch('/status', { headers: pinHeaders() });
                      if (response.status === 401) {
                        document.cookie = 'pin=; Path=/; Max-Age=0';
                        location.reload();
                        return;
                      }
                      if (!response.ok) {
                        return;
                      }
                      var data = await response.json();
                      if (data.camera === 'front' || data.camera === 'back') {
                        if (cameraFacing !== data.camera) {
                          userFlip = false;
                        }
                        cameraFacing = data.camera;
                        layoutVideo();
                      }
                      if (typeof data.battery === 'number') {
                        battery.textContent = 'Заряд телефона ' + data.battery + '%';
                      } else {
                        battery.textContent = 'Заряд телефона неизвестен';
                      }
                    } catch (error) {
                    }
                  }

                  async function connect() {
                    if (stopped) {
                      return;
                    }
                    var myGeneration = ++generation;
                    closePeer();
                    setWatchingUi(true);
                    setStatus('Подключение…', false);

                    var pin = cookiePin();
                    if (pin.length !== 4) {
                      location.reload();
                      return;
                    }

                    try {
                      pc = new RTCPeerConnection({
                        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
                      });
                      pc.addTransceiver('video', { direction: 'recvonly' });
                      pc.ontrack = function (event) {
                        video.srcObject = event.streams[0];
                        layoutVideo();
                      };
                      pc.oniceconnectionstatechange = function () {
                        if (!pc || myGeneration !== generation || stopped) {
                          return;
                        }
                        if (pc.iceConnectionState === 'failed' || pc.iceConnectionState === 'disconnected') {
                          scheduleReconnect('Связь оборвалась, переподключение…');
                        }
                      };

                      var offer = await pc.createOffer();
                      await pc.setLocalDescription(offer);
                      setStatus('Сбор адреса…', false);
                      await waitIceComplete(pc, 3000);
                      if (myGeneration !== generation || stopped) {
                        return;
                      }

                      setStatus('Ожидание камеры…', false);
                      var controller = new AbortController();
                      var fetchTimer = setTimeout(function () { controller.abort(); }, 20000);
                      var response;
                      try {
                        response = await fetch('/offer', {
                          method: 'POST',
                          headers: {
                            'Content-Type': 'application/json; charset=utf-8',
                            'X-Pin': pin
                          },
                          signal: controller.signal,
                          body: JSON.stringify({
                            type: pc.localDescription.type,
                            sdp: pc.localDescription.sdp
                          })
                        });
                      } finally {
                        clearTimeout(fetchTimer);
                      }

                      if (myGeneration !== generation || stopped) {
                        return;
                      }

                      if (response.status === 401) {
                        document.cookie = 'pin=; Path=/; Max-Age=0';
                        location.reload();
                        return;
                      }

                      if (!response.ok) {
                        scheduleReconnect('Ошибка offer: ' + response.status);
                        return;
                      }

                      var answer = await response.json();
                      if (!answer || !answer.sdp) {
                        scheduleReconnect('Нет ответа SDP, повтор…');
                        return;
                      }

                      await pc.setRemoteDescription(answer);
                      setStatus('Просмотр', false);
                    } catch (error) {
                      if (myGeneration !== generation || stopped) {
                        return;
                      }
                      scheduleReconnect('Сбой подключения, повтор…');
                    }
                  }

                  stopBtn.onclick = function () { stopWatch(); };
                  watchBtn.onclick = function () {
                    stopped = false;
                    connect();
                  };
                  flipBtn.onclick = function () {
                    userFlip = !userFlip;
                    layoutVideo();
                  };
                  cameraBtn.onclick = function () { switchCamera(); };
                  async function switchCamera() {
                    var pin = cookiePin();
                    if (pin.length !== 4) {
                      return;
                    }
                    try {
                      var response = await fetch('/camera', {
                        method: 'POST',
                        headers: pinHeaders()
                      });
                      if (response.status === 401) {
                        document.cookie = 'pin=; Path=/; Max-Age=0';
                        location.reload();
                        return;
                      }
                      if (!response.ok) {
                        return;
                      }
                      await refreshBattery();
                    } catch (error) {
                    }
                  }
                  stage.addEventListener('click', function (event) {
                    if (!isTouchUi() || event.target.closest('.actions')) {
                      return;
                    }
                    document.body.classList.toggle('controls-on');
                  });
                  window.addEventListener('pagehide', function () {
                    if (!stopped) {
                      hangup();
                    }
                  });
                  batteryTimer = setInterval(refreshBattery, 30000);
                  refreshBattery();
                  video.addEventListener('loadedmetadata', layoutVideo);
                  window.addEventListener('resize', layoutVideo);
                  connect();
                })();
              </script>
            </body>
            </html>
            """;
    }
}
