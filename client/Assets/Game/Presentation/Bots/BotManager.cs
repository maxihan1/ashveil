// 서버 봇 목록을 주기적으로 받아 봇 개체를 만들고·갱신하고, 플레이어와의 거리로 활성화한다
using System.Collections.Generic;
using System.Linq;
using Ashveil.Application;
using Ashveil.Shared.Dto;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class BotManager : MonoBehaviour
    {
        private const float PollSeconds = 15f;
        private const float ActiveDistance = 90f;

        private readonly Dictionary<long, BotController> _bots = new Dictionary<long, BotController>();
        private float _pollTimer;
        private float _activationTimer;
        private bool _polling;

        public IReadOnlyCollection<BotController> Bots => _bots.Values;

        public BotController Find(long id) => _bots.TryGetValue(id, out var bot) ? bot : null;

        public void RefreshSoon() => _pollTimer = 0;

        private void Update()
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer <= 0 && !_polling)
            {
                _pollTimer = PollSeconds;
                Poll();
            }

            _activationTimer -= Time.deltaTime;
            if (_activationTimer > 0) return;
            _activationTimer = 0.5f;
            var player = WorldServices.Player;
            if (player == null) return;
            foreach (var bot in _bots.Values)
            {
                if (bot.LocallyDead) continue;
                var near = Vector3.Distance(bot.transform.position, player.transform.position) < ActiveDistance;
                if (bot.gameObject.activeSelf != near) bot.gameObject.SetActive(near);
            }
        }

        private void Poll()
        {
            _polling = true;
            AsyncUi.Run(async () =>
            {
                try
                {
                    var list = await GameContext.Api.GetBotsAsync();
                    if (this != null) Sync(list);
                }
                finally
                {
                    _polling = false;
                }
            }, e => Debug.LogWarning("[Bots] " + e.Code + " " + e.Message));
        }

        private void Sync(List<BotDto> list)
        {
            var seen = new HashSet<long>();
            foreach (var dto in list)
            {
                seen.Add(dto.Id);
                if (_bots.TryGetValue(dto.Id, out var bot)) bot.Apply(dto);
                else _bots[dto.Id] = BotController.Create(dto, transform);
            }
            // GM 봇 리셋 등으로 사라진 봇은 지운다.
            foreach (var id in _bots.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                Destroy(_bots[id].gameObject);
                _bots.Remove(id);
            }
        }
    }
}
