var $tableTi = $('#tableTi');
var $removeTi = $('#delButtonTi');
var selectionsTi = [];
var maxIdTi = 0;

function getIdSelectionsTi() {
    return $.map($tableTi.bootstrapTable('getSelections'),
        function (row) {
            return row.Id;
        });
}

$(function () {

	$tableTi.on('all.bs.table',
		function (e, name, args) {
			//TODO optimize? too many calls
			let ids = $tableTi.bootstrapTable('getData').map(a => a.Id);
			maxIdTi = Math.max.apply(Math, ids);
		});

    $tableTi.on('check.bs.table uncheck.bs.table ' +
        'check-all.bs.table uncheck-all.bs.table',
        function () {
            $removeTi.prop('disabled', !$tableTi.bootstrapTable('getSelections').length);

            // save your data, here just save the current page
            selectionsTi = getIdSelectionsTi();
            // push or splice the selections if you want to save all data selections
        });

    $('#addButtonTi').click(function () {
        maxIdTi++;
        debugger;
        $tableTi.bootstrapTable('insertRow',
            {
                index: 0,
                row: {
                    Id: maxIdTi,
                    Name: '',
                    Entities: '',
                    Roles: ''
                }
            });
    });

    $('#delButtonTi').click(function () {
        var ids = getIdSelectionsTi();
        $tableTi.bootstrapTable('remove',
            {
                field: 'Id',
                values: ids
            });
        $removeTi.prop('disabled', true);
    });

    $('#saveButtonTi').on('click',
        function (event) {
            event.preventDefault();
            $('#smErrorTi').addClass("d-none");

            debugger;

            var str2 = encodeURIComponent(JSON.stringify($tableTi.bootstrapTable('getData')));

            $("#smErrorTi").load('{postTimersUrl}' + str2,
                function (result) {
                    if (result.toLowerCase().startsWith('error')) {
                        $('#smErrorTi').removeClass("d-none");
                        $('#smErrorTi').text(result);
                    }
                });
        });

});