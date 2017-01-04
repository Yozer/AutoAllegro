$(function () {
    $('body.auction input#non_material_checkbox').change(function () {
        var nonMaterialWrapper = $('#non-material-wrapper');

        if ($(this).is(':checked')) {
            nonMaterialWrapper.removeClass('hidden');
        } else {
            nonMaterialWrapper.addClass('hidden');
        }
    })
});
