/// <binding Clean='clean' />
"use strict";

var gulp = require("gulp"),
    rimraf = require("gulp-rimraf"),
    concat = require("gulp-concat"),
    cssmin = require("gulp-cssmin"),
    uglify = require("gulp-uglify"),
    sass = require('gulp-sass'),
    addsrc = require('gulp-add-src');

var paths = {
    webroot: "./wwwroot/"
};

paths.js = paths.webroot + "js/main.js";
paths.minJs = paths.webroot + "js/main.min.js";
paths.css = paths.webroot + "css/main.css";
paths.minCss = paths.webroot + "css/main.min.css";
paths.sass = paths.webroot + "css/**/*.scss";
paths.scssDest = paths.webroot + "css/";

gulp.task("clean:js", function (cb) {
    gulp.src(paths.minJs, { read: false })
        .pipe(rimraf());
});

gulp.task("clean:css", function (cb) {
    gulp.src([paths.css, paths.minCss], { read: false })
        .pipe(rimraf());
});



gulp.task('compile:sass', function () {
    gulp.src(paths.sass)
        .pipe(sass())
        .pipe(gulp.dest(paths.scssDest));
});



gulp.task("min:js", function () {
    return gulp.src([paths.js, "!" + paths.minJs], { base: "." })
        .pipe(addsrc(paths.webroot + "lib/clipboard/clipboard.js"))
        .pipe(addsrc(paths.webroot + "lib/jquery-popconfirm/jquery.popconfirm.js"))
        .pipe(uglify())
        .pipe(addsrc(paths.webroot + "lib/chart-js/chart.min.js"))
        .pipe(addsrc(paths.webroot + "lib/jquery-validation/jquery.validate.min.js"))
        .pipe(addsrc(paths.webroot + "lib/jquery-validation-unobtrusive/jquery-validation-unobtrusive.min.js"))
        .pipe(addsrc(paths.webroot + "lib/bootstrap/bootstrap.min.js"))
        .pipe(concat(paths.minJs))
        .pipe(gulp.dest("."));
});

gulp.task("min:css", function () {
    return gulp.src([paths.css, "!" + paths.minCss])
        .pipe(cssmin())
        .pipe(addsrc(paths.webroot + "lib/bootstrap//bootstrap-custom.min.css"))
        .pipe(concat(paths.minCss))
        .pipe(gulp.dest("."));
});


gulp.task("clean", ["clean:js", "clean:css"]);
gulp.task("compile", ["compile:sass"]);
gulp.task("min", ["min:js", "min:css"]);
gulp.task("process", ["clean", "compile", "min"]);
