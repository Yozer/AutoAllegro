$(function () {
    $('body.auction input#non_material_checkbox').change(function() {
        var nonMaterialWrapper = $('#non-material-wrapper');

        if ($(this).is(':checked')) {
            nonMaterialWrapper.removeClass('hidden');
        } else {
            nonMaterialWrapper.addClass('hidden');
        }
    });

    new Clipboard('.clipboard-copy');

    $("[data-confirm='default']").popConfirm({
        title: "Potwierdzenie",
        content: "Czy na pewno chcesz to zrobić?",
        placement: "bottom",
        container: "body",
        yesBtn: "Tak",
        noBtn: "Nie"
    });
});

