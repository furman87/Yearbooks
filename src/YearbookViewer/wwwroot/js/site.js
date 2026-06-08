// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Yearbook Viewer - Site JavaScript

// Debug logging
window.addEventListener('load', function() {
    console.log('Yearbook Viewer loaded');
    console.log('PhotoSwipeLightbox available:', typeof PhotoSwipeLightbox !== 'undefined');
    console.log('jQuery available:', typeof $ !== 'undefined');
});

// Global error handler for debugging
window.addEventListener('error', function(e) {
    console.error('JavaScript error:', e.error);
});

// Helper function to test PhotoSwipe
window.testPhotoSwipe = function() {
    console.log('Testing PhotoSwipe...');
    const gallery = document.getElementById('photo-gallery');
    if (gallery) {
        const links = gallery.querySelectorAll('a.photo-item-div');
        console.log('Found', links.length, 'photo links');
        
        if (typeof PhotoSwipeLightbox !== 'undefined') {
            console.log('PhotoSwipeLightbox is available');
            
            // Try to initialize a test lightbox
            try {
                const testLightbox = new PhotoSwipeLightbox({
                    gallery: '#photo-gallery',
                    children: 'a',
                    initialZoomLevel: 'fit',
                    showHideAnimationType: 'none',
                    pswpModule: PhotoSwipe
                });
                console.log('Test lightbox created successfully');
            } catch (error) {
                console.error('Error creating test lightbox:', error);
            }
        } else {
            console.error('PhotoSwipeLightbox is not available');
        }
    } else {
        console.log('No photo gallery found on this page');
    }
};
