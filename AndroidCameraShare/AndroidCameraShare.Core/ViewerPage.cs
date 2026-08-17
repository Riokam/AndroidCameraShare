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
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <link rel="icon" href="data:,">
              <title>Няня</title>
              <style>
                html, body { height: 100%; }
                body {
                  font-family: sans-serif;
                  margin: 0;
                  padding: 12px;
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
                .bar { flex-shrink: 0; padding-top: 12px; }
                #status { margin: 0; min-height: 1.5em; }
                #battery { margin: 4px 0 0; color: #aaa; }
                .error { color: #f66; }
                .actions { margin-top: 12px; display: flex; gap: 8px; flex-wrap: wrap; }
                button { font-size: 16px; padding: 8px 16px; }
              </style>
            </head>
            <body>
              <div class="stage">
                <video id="video" autoplay playsinline></video>
              </div>
              <div class="bar">
                <p id="status"></p>
                <p id="battery"></p>
                <div class="actions">
                  <button id="stop" type="button">Остановить просмотр</button>
                  <button id="watch" type="button" hidden>Смотреть</button>
                </div>
              </div>
              <script>
                (function () {
                  var video = document.getElementById('video');
                  var status = document.getElementById('status');
                  var battery = document.getElementById('battery');
                  var stopBtn = document.getElementById('stop');
                  var watchBtn = document.getElementById('watch');
                  var pc = null;
                  var reconnectTimer = null;
                  var batteryTimer = null;
                  var generation = 0;
                  var stopped = false;
                  var cameraFacing = 'back';

                  function layoutVideo() {
                    var stage = video.parentElement;
                    if (!stage) {
                      return;
                    }
                    var portrait = video.videoHeight > video.videoWidth;
                    var rotate = cameraFacing === 'back' && portrait && video.videoHeight > 0;
                    video.classList.toggle('landscape-rotate', rotate);
                    if (!rotate) {
                      video.style.width = '100%';
                      video.style.height = '100%';
                      video.style.transform = '';
                      return;
                    }
                    var sw = stage.clientWidth;
                    var sh = stage.clientHeight;
                    video.style.width = sh + 'px';
                    video.style.height = sw + 'px';
                    video.style.transform = 'translate(-50%, -50%) rotate(90deg)';
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
