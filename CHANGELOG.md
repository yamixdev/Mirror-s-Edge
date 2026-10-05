# Changes / Изменения

[English README](README.md) | [README на русском](README.ru.md) | [Open tasks / Задачи](TODO.md)

## 2026-10-05 - First update of this fork / Первое обновление форка

Based on Eideren's Still Alive project. Existing upstream history and authorship are retained. The entries below describe changes in this fork, not the creation of the original port.

Основа - Still Alive от Eideren. Его история Git и авторство сохранены. Ниже перечислены изменения этого форка, а не создание исходного порта.

| Area | Change | Изменение |
| --- | --- | --- |
| Unity | Project and packages updated to 6000.6.4f1; editor gameplay works in the tested cases. | Проект и пакеты обновлены до 6000.6.4f1; игра в редакторе работает в проверенных сценариях. |
| Test map | Added `Assets/New Scene.unity` with Spawn, obstacles and four parkour object types; basic materials need no extra asset pack. | Добавлена `Assets/New Scene.unity` со Spawn, препятствиями и четырьмя видами объектов паркура; базовые материалы не требуют отдельного набора ассетов. |
| Collision | Corrected initial-overlap handling to stop repeated wall-blocked steps from pushing the character upward. Low steps remain climbable in the regression case. | Исправлена обработка начального пересечения: повторные попытки шага у стены не поднимают персонажа. В проверке сохраняется подъём на низкую ступеньку. |
| Queries | Physics hit buffers grow when full instead of silently dropping hits beyond 16. | Буферы физических запросов расширяются при заполнении, вместо потери попаданий после первых 16. |
| Volumes | Destroyed volumes unregister their world callbacks; callback iteration tolerates removal during an update. | Уничтоженные volume удаляют свои колбэки мира; обход выдерживает удаление во время обновления. |
| Animations | End-pose sampling uses the final frame; destroyed Animator graphs are removed from the cache and invalid graphs can be recreated. | Конечная поза берётся из последнего кадра; графы уничтоженных Animator удаляются из кэша, недействительные графы пересоздаются. |
| Hands | Bounds update for externally driven first-person meshes prevents raised hands from disappearing when looking at a ledge. | Обновление границ моделей от первого лица убирает исчезновение поднятых рук при прямом взгляде на уступ. |
| Pull-up | Ledge pull-ups use a collision-checked route without the backward displacement from imported root motion. Low-ceiling and blocked routes are checked. | Подъём с уступа использует путь с проверкой столкновений без смещения назад из импортированного root motion. Проверяются низкий потолок и заблокированный путь. |
| Vault | Holding Space through a one-left-hand speed vault gives one upward jump while preserving forward speed. No repeated midair jump. | Удержание пробела до конца speed vault на одной левой руке даёт один прыжок вверх с сохранением скорости вперёд. Повторного прыжка в воздухе нет. |
| Death | Removed the legacy jump callback that scheduled suicide after a random 5-15 second delay; pending callbacks are cancelled. Normal fall damage remains. | Убран старый колбэк прыжка, назначавший смерть через случайные 5-15 секунд; ожидающие вызовы отменяются. Обычный урон от падения сохраняется. |
| Objects | Added BalanceBeam, SwingBar, Ladder and Zipline prefabs, an object menu, and geometry/interaction-volume rebuild controls. | Добавлены префабы BalanceBeam, SwingBar, Ladder и Zipline, меню объектов и обновление геометрии вместе с зоной взаимодействия. |
| Swing | Finite grip limits keep both hands within the bar. Approach zones and momentum transfer support tested catches on the next bar. | Ограничения захвата удерживают обе руки в пределах перекладины. Зоны подхода и перенос скорости позволяют захватить следующую перекладину в проверенных случаях. |
| Swing animation | Ground vault selection is suppressed while attached to a bar. Missing third-person `JumpAir` falls back to `JumpSlow`; the existing first-person clip is retained. | Наземный vault не выбирается во время захвата перекладины. При отсутствии `JumpAir` у полной модели используется `JumpSlow`; существующий клип от первого лица сохраняется. |
| Input | Releasing Shift clears crouch requests even during parkour input restrictions or after a lost Game-view key-up event. | Отпускание Shift снимает запрос приседа при блокировке ввода паркуром и после потери события отпускания в окне Game. |
| Audio | Swing sound fades during stationary hanging. Sideways hand-contact notifies run in both playback directions for `SwingStrafe`. | Звук раскачивания затихает на неподвижном висе. События контакта рук при боковом движении работают в обоих направлениях воспроизведения `SwingStrafe`. |
| Shimmy | First-person hand placement follows separate release/replant phases; the arm solver preserves animated roll and avoids feedback from the previous IK result. | Руки от первого лица используют отдельные фазы отпускания и нового захвата; расчёт положения сохраняет вращение из анимации и не использует прошлый результат IK как новую основу. |
| Full body | Enabled the existing full model for external cameras and shadows; restored native root scale instead of the imported 2.54 scale. Owner camera keeps the first-person meshes. | Включена существующая полная модель для внешних камер и теней; восстановлен исходный масштаб корня вместо импортированного 2,54. Игровая камера сохраняет модели от первого лица. |
| Materials | Included 15 restored Faith textures and eight materials. The optional material restoration menu remains available for development. | Добавлены 15 восстановленных текстур Фэйт и восемь материалов. Меню восстановления материалов оставлено для разработки. |
| Documentation | Added English/Russian setup, object guides, verification instructions and an expanded work list with Eideren's original tasks. | Добавлены установка, инструкции по объектам и проверкам на английском и русском, а также расширенный список задач с исходными пунктами Eideren. |

## Verification / Проверки

Checks use real Unity physics, the converted world and imported animation resources in a separate test project. They cover selected cases of collisions, deletion and animation cleanup, hand visibility, pull-ups, vault jumps, delayed survival, parkour objects, swinging, sideways hand motion and full-body rendering. Some render checks need a graphics adapter. Test source and commands are in [Tools/CollisionProbe](Tools/CollisionProbe/README.md).

Проверки используют физику Unity, перенесённый игровой мир и импортированные анимации в отдельном тестовом проекте. Проверяются отдельные случаи столкновений, удаления объектов и очистки анимаций, видимости рук, подъёма, прыжков после vault, выживания после прыжка, объектов паркура, раскачивания, бокового движения рук и полной модели. Для части проверок отрисовки нужна видеокарта. Исходники и команды находятся в [Tools/CollisionProbe](Tools/CollisionProbe/README.ru.md).

This is not a completed controller rewrite or proof of every map working. Ladder failures and other outstanding work are recorded in [TODO.md](TODO.md). Full standalone builds, complete map playthroughs and a frame-rate matrix remain untested.

Контроллер ещё не доведён до конца; проверки не подтверждают работу каждой карты. Ошибки лестницы и остальные задачи перечислены в [TODO.md](TODO.md). Полные отдельные сборки, прохождение всех карт и набор проверок на разных частотах кадров пока не выполнены.
