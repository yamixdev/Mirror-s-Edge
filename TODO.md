# Work left / Что ещё сделать

[English README](README.md) | [README на русском](README.ru.md) | [Completed changes / Сделанное](CHANGELOG.md)

## English

This list keeps the original author's outstanding work. A fix for one reported case does not close the whole subject. New tasks can be added as testing continues.

| Priority | Task | Current state |
| --- | --- | --- |
| High | Make ladder entry safe when an animation is missing. | Observed `NullReferenceException` in `TdMove_IntoClimb.StartClimbMove`: `AnimSeq1p.AnimSeq` can be null after missing `LadderClimbHangStartHard`. Not fixed. Check entry, exit and crouch recovery. |
| High | Check frame-rate dependence. | The original README reports problems above 62 FPS. No fixed gameplay step has been implemented; compare 30/60/120/144 FPS before deciding how to separate simulation and rendering. |
| Medium | Improve step-up and slopes. | Wall jitter and a 20 cm step regression case are addressed. The original overly permissive step-up and rigid incline behavior still need comparison with the game. |
| Medium | Investigate intermittent player initialization exceptions. | Eideren reported a null reference in the `_3pPlayer == null` branch of `PawnLink_Unity.Update`. Preview-mesh destruction still needs guards and repeated spawn/teardown tests. |
| Medium | Fix remaining stuck/floating cases. | Retained from the original README, which also notes similar behavior in the game. Reproduction cases are needed. |
| Medium | Improve full-body grip and arm alignment. | First-person shimmy was corrected. Third-person clips still need their own contact/shoulder checks; a full third-person hand solver has not been added. |
| Medium | Review missing animation and native-call diagnostics. | `standready`, `TestAnim`, force-feedback and NaN diagnostics have appeared during testing. Separate unused placeholders from actual broken moves. |
| Medium | Compare sound behavior with the original. | Stationary swing noise and left/right contact events were addressed. The author described the broader audio port as an interpretation; remaining cues and mixes need review. |
| Medium | Check a clean install and standalone build across complete maps. | Targeted editor checks are available. No complete playthrough or standalone release validation yet. |
| Later | Verify other Unity versions and render pipelines. | Tested on 6000.6.4f1 with Built-in rendering. Full-body camera callbacks need adaptation before URP/HDRP. |
| Later | Investigate reusable Unity packages. | Original task; not implemented. |
| Later | Import UE3 maps. | Original task. [Cartographer](https://github.com/Eideren/Cartographer) is a reference for related UE4 work; it is not an importer integrated here. |
| Later | Extract and import materials from `.upk`. | Original task: T3D loses properties. References: [Eideren's Unreal Library fork](https://github.com/Eideren/Unreal-Library) and [UDKImportPlugin](https://github.com/Eideren/UDKImportPlugin). The Faith texture restoration tool is a small, specific helper, not this general importer. |
| Later | Connect interactable buttons to Unity events. | Original task; not implemented. |

Combat, AI, story events, interface and saves are outside the current work. Do not treat them as completed features of this fork.

## Русский

Здесь сохранены незакрытые задачи исходного автора. Исправление одного случая не закрывает всю тему. По мере проверок список будет пополняться.

| Приоритет | Задача | Текущее состояние |
| --- | --- | --- |
| Высокий | Безопасный вход на лестницу при отсутствии анимации. | Найден `NullReferenceException` в `TdMove_IntoClimb.StartClimbMove`: после отсутствующего `LadderClimbHangStartHard` поле `AnimSeq1p.AnimSeq` может быть null. Не исправлено. Проверить вход, выход и восстановление после приседа. |
| Высокий | Проверить зависимость от частоты кадров. | В исходном README отмечены проблемы выше 62 FPS. Фиксированный шаг игрового мира не введён; сравнить 30/60/120/144 FPS и затем решать, как разделить симуляцию и отрисовку. |
| Средний | Доработать шаг и склоны. | Дрожание у стены и проверка ступеньки 20 см исправлены. Слишком свободный подъём на препятствия и жёсткое движение по склонам ещё нужно сравнить с игрой. |
| Средний | Разобраться со случайными ошибками создания персонажа. | Eideren отмечал null reference в ветке `_3pPlayer == null` функции `PawnLink_Unity.Update`. Удаление preview-модели ещё требует проверок и повторных циклов создания/удаления персонажа. |
| Средний | Исправить оставшиеся застревания и зависания в воздухе. | Пункт сохранён из исходного README, где отмечалось похожее поведение и в игре. Нужны воспроизводимые примеры. |
| Средний | Доработать захват и положение рук полной модели. | Боковое движение рук от первого лица исправлено. Клипам от третьего лица ещё нужны проверки контакта и плеч; отдельный полноценный расчёт захвата для них не добавлен. |
| Средний | Разобрать сообщения об отсутствующих анимациях и нативных вызовах. | При проверках встречались `standready`, `TestAnim`, force-feedback и сообщения о NaN. Отделить неиспользуемые заглушки от действительно сломанных движений. |
| Средний | Сравнить звук с оригиналом. | Исправлены шум на неподвижном висе и события контакта при движении влево/вправо. Автор описывал звук порта как приближение; остальные звуки и их сочетания ещё требуют проверки. |
| Средний | Проверить чистую установку и отдельную сборку с полными картами. | Есть отдельные проверки редактора. Полного прохождения и проверки самостоятельной версии пока нет. |
| Позже | Проверить другие версии Unity и способы отрисовки. | Проверено на 6000.6.4f1 с Built-in. Для URP/HDRP нужно адаптировать колбэки камер полной модели. |
| Позже | Разобраться с выпуском Unity packages. | Исходная задача; не сделано. |
| Позже | Импортировать карты UE3. | Исходная задача. [Cartographer](https://github.com/Eideren/Cartographer) можно использовать как пример работы для UE4; в этот проект импортёр не встроен. |
| Позже | Извлекать и импортировать материалы из `.upk`. | Исходная задача: T3D теряет свойства. Примеры: [форк Unreal Library от Eideren](https://github.com/Eideren/Unreal-Library) и [UDKImportPlugin](https://github.com/Eideren/UDKImportPlugin). Восстановление текстур Фэйт решает только один частный случай. |
| Позже | Подключить интерактивные кнопки к Unity events. | Исходная задача; не сделано. |

Боёвка, AI, сюжетные события, интерфейс и сохранения вне текущей работы. Они не считаются готовыми возможностями форка.
