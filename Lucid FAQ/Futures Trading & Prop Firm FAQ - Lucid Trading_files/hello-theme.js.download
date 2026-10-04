(function( $ ) {
	'use strict';
    function changeBillingAddressLabel() {
        var addressField = $('#billing_address_1_field label');
        if (addressField.length) {
            addressField.html('Address <abbr class="required" title="required">*</abbr>');
        }
    }
    // Change label on page load
    changeBillingAddressLabel();

    // Change label after AJAX update
    $(document.body).on('updated_checkout', function() {
        changeBillingAddressLabel();
    });

})( jQuery );
