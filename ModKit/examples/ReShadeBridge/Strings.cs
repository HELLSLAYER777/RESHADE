using System;
using System.Collections.Generic;
using System.Globalization;

namespace ReShadeBridge
{
    /// <summary>The mod's texts: English, French, Arabic (the app's language decides).</summary>
    internal static class Strings
    {
        // key -> { en, fr, ar }
        private static readonly Dictionary<string, string[]> Table = new()
        {
            ["title"] = new[] { "ReShade on generated frames", "ReShade sur les images générées", "ReShade على الفريمات المولَّدة" },
            ["intro"] = new[]
            {
                "ReShade runs inside the frame-generation engine, so its effects and its overlay apply to every frame you see, real and generated. Press {0} over the game to open the ReShade overlay (the game steps back while it is open); press {0} again to return to the game.",
                "ReShade s'exécute dans le moteur de génération d'images : ses effets et son overlay s'appliquent à chaque image affichée, réelle ou générée. Appuyez sur {0} dans le jeu pour ouvrir l'overlay ReShade (le jeu passe au second plan tant qu'il est ouvert) ; appuyez de nouveau sur {0} pour revenir au jeu.",
                "يعمل ReShade داخل محرّك توليد الفريمات، فتنطبق مؤثراته وواجهته على كل فريم تراه، الحقيقي والمولَّد. اضغط {0} فوق اللعبة لفتح واجهة ReShade (تتراجع اللعبة إلى الخلف أثناء فتحها)، واضغط {0} مرة أخرى للعودة إلى اللعبة."
            },
            ["lblDll"] = new[] { "ReShade DLL: ReShade64.dll, or the dxgi.dll of a ReShade you already use", "DLL ReShade : ReShade64.dll, ou le dxgi.dll d'un ReShade que vous utilisez déjà", "ملف ReShade: ‏ReShade64.dll أو dxgi.dll من ReShade تستخدمه بالفعل" },
            ["lblFolder"] = new[] { "ReShade folder: the one with reshade-shaders, your preset and ReShade.ini (optional)", "Dossier ReShade : celui qui contient reshade-shaders, votre preset et ReShade.ini (facultatif)", "مجلد ReShade: الذي فيه reshade-shaders والإعداد المسبق وReShade.ini (اختياري)" },
            ["lblEngine"] = new[] { "Engine folder: the folder with the engine's .exe", "Dossier du moteur : le dossier qui contient le .exe du moteur", "مجلد المحرّك: المجلد الذي فيه ملف .exe للمحرّك" },
            ["lblKey"] = new[] { "Overlay key", "Touche de l'overlay", "زر الواجهة" },
            ["lblLoad"] = new[] { "Load ReShade as", "Charger ReShade en tant que", "تحميل ReShade باسم" },
            ["hintLoad"] = new[] { "dxgi.dll works for DirectX 11 and 12 engines. Change it only if the ReShade log says it did not load.", "dxgi.dll convient aux moteurs DirectX 11 et 12. Ne le changez que si le journal ReShade indique qu'il ne s'est pas chargé.", "يناسب dxgi.dll محرّكات DirectX 11 و12. غيّره فقط إذا قال سجل ReShade إنه لم يُحمَّل." },
            ["chkEnabled"] = new[] { "Overlay key bridge on", "Pont de la touche activé", "تفعيل جسر زر الواجهة" },
            ["chkFront"] = new[] { "React only when the game is in front", "Réagir seulement quand le jeu est au premier plan", "الاستجابة فقط عندما تكون اللعبة في المقدمة" },
            ["btnBrowse"] = new[] { "Browse…", "Parcourir…", "استعراض…" },
            ["btnDetect"] = new[] { "Detect", "Détecter", "اكتشاف" },
            ["btnInstall"] = new[] { "Install in engine", "Installer dans le moteur", "تثبيت في المحرّك" },
            ["btnRemove"] = new[] { "Remove from engine", "Retirer du moteur", "إزالة من المحرّك" },
            ["btnOpen"] = new[] { "Open overlay now", "Ouvrir l'overlay maintenant", "افتح الواجهة الآن" },
            ["btnGet"] = new[] { "Get ReShade", "Obtenir ReShade", "احصل على ReShade" },
            ["btnLog"] = new[] { "Show ReShade log", "Afficher le journal ReShade", "عرض سجل ReShade" },
            ["btnSettings"] = new[] { "Settings", "Réglages", "الإعدادات" },
            ["cardTitle"] = new[] { "ReShade overlay", "Overlay ReShade", "واجهة ReShade" },
            ["cardLine"] = new[] { "{0} opens it over the game", "{0} l'ouvre dans le jeu", "{0} يفتحها فوق اللعبة" },
            ["cardNotLoaded"] = new[] { "not loaded in the engine", "non chargé dans le moteur", "غير محمَّل في المحرّك" },
            ["cardLoaded"] = new[] { "ReShade loaded", "ReShade chargé", "ReShade محمَّل" },

            ["stEngineOff"] = new[] { "Engine: not running", "Moteur : à l'arrêt", "المحرّك: متوقف" },
            ["stEngineRun"] = new[] { "Engine: running (PID {0}, {1})", "Moteur : en marche (PID {0}, {1})", "المحرّك: يعمل (PID {0}، {1})" },
            ["stWindow"] = new[] { "Output window: found", "Fenêtre de sortie : trouvée", "نافذة الإخراج: موجودة" },
            ["stNoWindow"] = new[] { "Output window: not found yet (pick a game first)", "Fenêtre de sortie : pas encore trouvée (choisissez d'abord un jeu)", "نافذة الإخراج: لم توجد بعد (اختر لعبة أولاً)" },
            ["stFile"] = new[] { "Engine folder: ReShade {0} installed as {1}", "Dossier du moteur : ReShade {0} installé sous le nom {1}", "مجلد المحرّك: ReShade {0} مثبَّت باسم {1}" },
            ["stNoFile"] = new[] { "Engine folder: ReShade not installed", "Dossier du moteur : ReShade non installé", "مجلد المحرّك: ReShade غير مثبَّت" },
            ["stLoaded"] = new[] { "ReShade inside the engine: loaded", "ReShade dans le moteur : chargé", "ReShade داخل المحرّك: محمَّل" },
            ["stNotLoaded"] = new[] { "ReShade inside the engine: not loaded (install it, then stop and start motion)", "ReShade dans le moteur : non chargé (installez-le, puis arrêtez et relancez le mouvement)", "ReShade داخل المحرّك: غير محمَّل (ثبّته ثم أوقف الحركة وشغّلها)" },
            ["stUnknown"] = new[] { "ReShade inside the engine: unknown", "ReShade dans le moteur : inconnu", "ReShade داخل المحرّك: غير معروف" },
            ["stOverlayOpen"] = new[] { "Overlay: open (the engine window has the keyboard and the mouse)", "Overlay : ouvert (la fenêtre du moteur a le clavier et la souris)", "الواجهة: مفتوحة (نافذة المحرّك تملك لوحة المفاتيح والفأرة)" },
            ["stOverlayClosed"] = new[] { "Overlay: closed", "Overlay : fermé", "الواجهة: مغلقة" },

            ["ok_installed"] = new[] { "ReShade installed in {0}. Stop and start motion for it to load.", "ReShade installé dans {0}. Arrêtez puis relancez le mouvement pour le charger.", "تم تثبيت ReShade في {0}. أوقف الحركة وشغّلها ليُحمَّل." },
            ["ok_removed"] = new[] { "ReShade removed from {0}. Stop and start motion.", "ReShade retiré de {0}. Arrêtez puis relancez le mouvement.", "تمت إزالة ReShade من {0}. أوقف الحركة وشغّلها." },
            ["err_nodll"] = new[] { "Choose the ReShade DLL first.", "Choisissez d'abord la DLL ReShade.", "اختر ملف ReShade أولاً." },
            ["err_notreshade"] = new[] { "That file is not a ReShade DLL.", "Ce fichier n'est pas une DLL ReShade.", "هذا الملف ليس ملف ReShade." },
            ["err_noengine"] = new[] { "Choose the engine folder first (start motion once, then press Detect).", "Choisissez d'abord le dossier du moteur (lancez le mouvement une fois, puis cliquez sur Détecter).", "اختر مجلد المحرّك أولاً (شغّل الحركة مرة ثم اضغط اكتشاف)." },
            ["err_nofolder"] = new[] { "The folder {0} does not exist.", "Le dossier {0} n'existe pas.", "المجلد {0} غير موجود." },
            ["err_foreign"] = new[] { "{0} already exists in the engine folder and is not ReShade. Nothing was changed.", "{0} existe déjà dans le dossier du moteur et ce n'est pas ReShade. Rien n'a été modifié.", "الملف {0} موجود في مجلد المحرّك وليس ReShade. لم يتغير شيء." },
            ["err_locked"] = new[] { "The file is in use. Stop motion first, then try again.", "Le fichier est utilisé. Arrêtez d'abord le mouvement, puis réessayez.", "الملف قيد الاستخدام. أوقف الحركة أولاً ثم أعد المحاولة." },
            ["err_access"] = new[] { "No permission to write in {0}. Run the app as administrator, or use a folder you can write to.", "Pas l'autorisation d'écrire dans {0}. Lancez l'application en administrateur ou utilisez un dossier modifiable.", "لا توجد صلاحية للكتابة في {0}. شغّل التطبيق كمسؤول أو استخدم مجلداً يمكنك الكتابة فيه." },
            ["err_arch"] = new[] { "The ReShade DLL is {0} but the engine is {1}.", "La DLL ReShade est en {0} mais le moteur est en {1}.", "ملف ReShade من نوع {0} بينما المحرّك من نوع {1}." },
            ["err_io"] = new[] { "Could not finish: {0}", "Impossible de terminer : {0}", "تعذّر الإكمال: {0}" },
            ["info_nothing"] = new[] { "No ReShade to remove.", "Aucun ReShade à retirer.", "لا يوجد ReShade لإزالته." },
            ["info_detected"] = new[] { "Engine folder: {0}", "Dossier du moteur : {0}", "مجلد المحرّك: {0}" },
            ["info_guessed"] = new[] { "The engine is not running. Its folder is probably {0}; check it, or start motion once and press Detect again.", "Le moteur n'est pas lancé. Son dossier est probablement {0} ; vérifiez-le, ou lancez le mouvement une fois et cliquez de nouveau sur Détecter.", "المحرّك غير شغال. مجلده على الأرجح {0}؛ تأكد منه، أو شغّل الحركة مرة واضغط اكتشاف من جديد." },
            ["info_nodetect"] = new[] { "The engine was not found. Press START MOTION, then press Detect.", "Moteur introuvable. Lancez START MOTION puis cliquez sur Détecter.", "لم يوجد المحرّك. اضغط START MOTION ثم اضغط اكتشاف." },
            ["info_nowindow_block"] = new[] { "The engine's output window was not found. Is motion running with a game picked?", "La fenêtre de sortie du moteur est introuvable. Le mouvement est-il lancé avec un jeu choisi ?", "لم توجد نافذة إخراج المحرّك. هل الحركة تعمل ومعها لعبة مختارة؟" },
            ["info_notloaded_block"] = new[] { "ReShade is not loaded in the engine, so there is no overlay to open. Install it, then stop and start motion.", "ReShade n'est pas chargé dans le moteur, il n'y a donc pas d'overlay à ouvrir. Installez-le, puis arrêtez et relancez le mouvement.", "ReShade غير محمَّل في المحرّك فلا توجد واجهة لفتحها. ثبّته ثم أوقف الحركة وشغّلها." },
            ["info_focus"] = new[] { "Windows would not bring the engine window to the front. Click on the game picture once and try again.", "Windows n'a pas mis la fenêtre du moteur au premier plan. Cliquez une fois sur l'image du jeu puis réessayez.", "لم يسمح ويندوز بإحضار نافذة المحرّك إلى المقدمة. انقر مرة على صورة اللعبة ثم أعد المحاولة." },
            ["info_nolog"] = new[] { "No ReShade.log in the engine folder yet. ReShade writes it when the engine loads it.", "Pas encore de ReShade.log dans le dossier du moteur. ReShade l'écrit quand le moteur le charge.", "لا يوجد ReShade.log في مجلد المحرّك بعد. يكتبه ReShade عندما يحمّله المحرّك." },
            ["info_saved"] = new[] { "Saved.", "Enregistré.", "تم الحفظ." },
        };

        public static string Get(string language, string key, params object[] args)
        {
            var index = language == "ar" ? 2 : language == "fr" ? 1 : 0;
            if (!Table.TryGetValue(key, out var row)) return key;
            var text = row[index];
            return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
        }
    }
}
