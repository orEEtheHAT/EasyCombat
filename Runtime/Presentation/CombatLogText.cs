using System.Text;

namespace EasyCombat.Presentation
{
    /// <summary>Presentation only. Call at a throttled UI refresh, never from the damage calculation.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Demo", "GamesFactory.Combat.Demo")]
    public static class CombatLogText
    {
        public static string Reason(DamageRejection reason) => reason switch
        {
            DamageRejection.None => "Принято", DamageRejection.InvalidAmount => "Некорректный урон",
            DamageRejection.Dead => "Цель погибла", DamageRejection.Disabled => "Получатель выключен",
            DamageRejection.NoReceiver => "Нет получателя", DamageRejection.Immune => "Иммунитет", DamageRejection.NoEffect => "Без эффекта",
            DamageRejection.Self => "Самоурон запрещён", DamageRejection.Ally => "Союзный огонь запрещён",
            DamageRejection.Enemy => "Урон противников запрещён", DamageRejection.Neutral => "Нейтральный источник запрещён",
            DamageRejection.Environment => "Урон окружения запрещён", DamageRejection.Invulnerable => "Неуязвимость",
            DamageRejection.Dodged => "Уклонение", DamageRejection.Blocked => "Поглощено защитой",
            DamageRejection.InvalidDefense => "Ошибка защиты", DamageRejection.Reentrant => "Вложенное попадание отклонено", _ => reason.ToString()
        };
        public static string Short(CombatRecord record)
        {
            var result = record.Result;
            var outcome = result.HitApplied ? $"−{result.AppliedAmount:0.##} HP" + (result.ShieldAbsorbed > 0 ? $" · щит −{result.ShieldAbsorbed:0.##}" : "")
                : result.StatusesApplied > 0 ? $"статусов: {result.StatusesApplied}" : Reason(result.Rejection);
            return $"{record.Target.Name}: {outcome}{(result.Damage.IsCritical ? " · крит" : "")}{(result.Killed ? " · смерть" : result.Downed ? " · повержен" : "")}\n"
                + (result.Damage.Origin.StatusName ?? result.Damage.Origin.AttackName ?? "Окружение");
        }
        public static string Format(CombatRecord record)
        {
            var result = record.Result;
            var origin = result.Damage.Origin;
            var text = new StringBuilder(256);
            text.Append(record.Time.ToString("0.0")).Append(" с  ·  ")
                .Append(origin.Actor.Name ?? "Окружение").Append(" → ").Append(record.Target.Name ?? "Цель")
                .Append("  ·  −").Append(result.AppliedAmount.ToString("0.##")).Append(" HP");
            if (result.Damage.IsCritical) text.Append("  КРИТ");
            if (result.Damage.IsPeriodic) text.Append("  ТИК");
            if (result.Killed) text.Append("  УБИЙСТВО");
            if (result.Downed) text.Append("  ПОВЕРЖЕН");
            if (result.Rejection != DamageRejection.None) text.Append("  [").Append(Reason(result.Rejection)).Append(']');
            text.Append('\n').Append(origin.AttackName ?? "Прямое воздействие");
            if (!string.IsNullOrEmpty(origin.StatusName)) text.Append(" / ").Append(origin.StatusName);
            if (result.StatusesApplied > 0) text.Append("  ·  статусов: ").Append(result.StatusesApplied);
            if (result.Breakdown.Count > 0) text.Append("\nРезисты: ").Append(result.ResistanceDelta.ToString("0.##"))
                .Append(" · Броня/блок: ").Append(result.ArmorPrevented.ToString("0.##"))
                .Append(" · Щиты: ").Append(result.ShieldAbsorbed.ToString("0.##"));
            for (var i = 0; i < result.Breakdown.Count; i++)
            {
                var part = result.Breakdown[i];
                text.Append("  |  ").Append(part.Type != null ? part.Type.Label : "Без типа")
                    .Append(' ').Append(part.Incoming.ToString("0.##"))
                    .Append(" → ").Append(part.Effective.ToString("0.##"))
                    .Append(" → HP-вход ").Append(part.HealthDamage.ToString("0.##"))
                    .Append(" → ").Append(part.Applied.ToString("0.##"));
            }
            for (var i = 0; i < result.Defenses.Count; i++)
            {
                var layer = result.Defenses[i];
                text.Append('\n').Append(layer.Index).Append(". ").Append(layer.Name).Append(" [")
                    .Append(layer.Stage == DefenseStage.BeforeResistance ? "до резистов" : "после резистов").Append("]: −").Append(layer.Prevented.ToString("0.##"));
                if (layer.IsShield) text.Append(" · осталось ").Append(layer.Remaining.ToString("0.##"));
            }
            if (result.Overkill > 0) text.Append("  ·  лишний урон: ").Append(result.Overkill.ToString("0.##"));
            if(result.LifePrevented>0) text.Append("  ·  защита жизни: ").Append(result.LifePrevented.ToString("0.##"));
            return text.ToString();
        }
    }
}
