# API: точки входа

Namespace основных модулей — `EasyCombat`, asmdef — `EasyCombat.Runtime`. В собственном asmdef добавь ссылку на Runtime. Physics2D находится в отдельной `EasyCombat.Physics2D`, редакторские инструменты — в `EasyCombat.Editor`. Для компонентов и SO используй Inspector/Combat Studio; код нужен для ввода, AI, анимации и связей с правилами конкретной игры.

## Минимальный выстрел

```csharp
using EasyCombat;
using UnityEngine;

public sealed class FireInput : MonoBehaviour
{
    public DamageEmitter Emitter;
    public Transform Muzzle;

    public bool FireAt(Vector3 worldPoint)
        => Emitter.TryFire(Muzzle.position, worldPoint - Muzzle.position);
}
```

Назначь `Emitter.Attack` SO оружия, `OwnerRoot` корень стрелка, а для projectile/impact-area — соответствующие пулы. У цели нужны Health + Collider, для статусов также StatusController. Hurtbox на дочернем коллайдере задаёт получателя и множитель зоны. Отдельный код пути урона для оружия не требуется.

Для урона по конкретной уже выбранной цели: `target.ApplyDamage(attack.RollRandom(source, point, direction))`. Это обходит поиск контакта/геометрию доставки, поэтому попадание в Hurtbox, взрыв или создание лужи само не произойдёт. `new DamageInfo(10)` подходит для явного простого запроса. Не вычитай Current вручную.

## Карта модулей

| Задача | API и состояние | Руководство |
|---|---|---|
| HP / защита / жизнь | `Health.ApplyDefinition`, `ApplyDamage`, `ApplyHealing`, `ResetToFull`, `Revive`; Current, Maximum, LifeState, Identity, Defense | [Основы](CombatGuide.ru.md), [Защита](AdvancedDefense.ru.md), [Жизнь](LifeAttribution.ru.md) |
| Характеристики | `CombatStats.SetValue`, `TryAdd`, `Remove(handle)`, `RemoveSource(owner)`; SO-ключи и принадлежащие владельцу модификаторы | [Stats](Stats.ru.md) |
| Ресурсы | `CombatResources.Configure`, `TryGet`, `TrySpend`, `Restore`, `RefreshMaximums`; стоимость нескольких ресурсов атомарна | [Ресурсы](ResourcesHealing.ru.md) |
| Статусы | `StatusController.Apply`, `ApplyFromHit`, `Remove`, `Dispel`, `TryConsume`, `TryCombine`; `GetStatus`, `GetStack`, `TryGetStackDamage` | [Статусы](StatusesControl.ru.md) |
| Реакции | `CombatReactions.Configure(ReactionDefinition[])`; условия/цель/actions задаются в SO | [Реакции](Reactions.ru.md) |
| Способности | `CombatAbilities.Configure`, `TryActivate`, `Cancel`; GetAbility, IsCasting, Casting, CastRemaining | [Abilities](AbilitiesDelivery.ru.md) |
| Доставка | `DamageEmitter.TryFire`; `ProjectilePool.Spawn/Prewarm/ClearActive`; `CombatMelee.Begin/SetAnchors/Stop`; CombatBeam и CombatAreaTrigger | [Доставка](AbilitiesDelivery.ru.md), [Снаряды/2D](ProjectileMotion2D.ru.md) |
| Сложная цель | CombatBodyPart, CombatBuildup, DamageRedirector | [Части тела](Composite.ru.md), [Защита](AdvancedDefense.ru.md) |
| Сохранение | `CombatSaveSystem.TryCaptureJson/TryRestoreJson`, CombatDefinitionCatalog, CombatReferenceRegistry | [Сохранения](Saving.ru.md) |
| Представление | `CombatPresentationRelay.Configure(hub)`; `CombatPresentationHub.Configure/Emitted/Clear`; CombatPresentationBuffer | [Представление](Presentation.ru.md) |
| Журнал | CombatJournal, CombatEvents.DamageResolved, локальное `Health.Resolved` | [Основы](CombatGuide.ru.md) |
| Диагностика | Combat Studio, Battle Calculator, Dependency Graph, Runtime Explanation | [Инструменты](AuthoringTools.ru.md) |

## Правила интеграции

- SO хранит общий рецепт, компонент — состояние конкретного актёра. Не меняй SO ради временного усиления одного персонажа. Используй runtime modifiers/handles, ресурсы и статусы.
- Identity содержит идентификатор **жизни**, который меняется при reset/reuse. Сохраняй его вместе с ссылкой, если откладываешь действие. `CombatActorIdentity.Capture(source)` также сохраняет имя для журнала после уничтожения объекта.
- `Health.Damaged` означает потерю HP. Щиты, блоки и промахи смотри в `Resolved`/DamageResult. Локальный Resolved приходит участникам (цели и источнику); фильтруй TargetHealth, если нужен только входящий урон.
- `Health.CaptureState/RestoreState` — компактный HP-снимок, не полное состояние боя. Полный JSON использует CombatSaveSystem. Игровой слой восстанавливает объекты/позиции и регистрирует actor ID до restore; летящие снаряды не сериализуются.
- Подписки снимай в OnDisable/OnDestroy. Наблюдатели изолированы от исключений, но геймплейные изменения из UI затрудняют предсказуемость. Реакции задавай через CombatReactions.
- Обычная игра использует встроенные планировщики. Ручной Advance/Simulate нужен для тестов и своего цикла времени; отключай автоматический тик там, где компонент поддерживает AutomaticAdvance, чтобы не получить двойное продвижение.
- Extension points: StatusTickEffect, DefenseLayerDefinition, CombatReactionAction, CombatAbilityAction. Пользовательская логика обязана соблюдать контракт валидации/подготовки и ограничивать выделения памяти; результаты пользовательских действий не становятся автоматически детерминированными или сериализуемыми.

Пределы массивов, бюджетов, гарантии и исключения описаны в руководстве соответствующего модуля. Сетевой authority/rollback, инвентарь, экономика, ввод и AI находятся за границами пакета.

