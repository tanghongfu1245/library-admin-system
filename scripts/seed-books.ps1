. (Join-Path $PSScriptRoot 'common.ps1')

$books = @(
    @{ Title='活着'; Author='余华'; Category='文学'; Publisher='作家出版社'; Year=2012 },
    @{ Title='许三观卖血记'; Author='余华'; Category='文学'; Publisher='作家出版社'; Year=2012 },
    @{ Title='平凡的世界'; Author='路遥'; Category='文学'; Publisher='北京十月文艺出版社'; Year=2017 },
    @{ Title='白鹿原'; Author='陈忠实'; Category='文学'; Publisher='人民文学出版社'; Year=2012 },
    @{ Title='围城'; Author='钱钟书'; Category='文学'; Publisher='人民文学出版社'; Year=1991 },
    @{ Title='边城'; Author='沈从文'; Category='文学'; Publisher='北京十月文艺出版社'; Year=2017 },
    @{ Title='骆驼祥子'; Author='老舍'; Category='文学'; Publisher='人民文学出版社'; Year=2012 },
    @{ Title='四世同堂'; Author='老舍'; Category='文学'; Publisher='人民文学出版社'; Year=2012 },
    @{ Title='茶馆'; Author='老舍'; Category='文学'; Publisher='人民文学出版社'; Year=2012 },
    @{ Title='红高粱家族'; Author='莫言'; Category='文学'; Publisher='浙江文艺出版社'; Year=2017 },
    @{ Title='蛙'; Author='莫言'; Category='文学'; Publisher='浙江文艺出版社'; Year=2017 },
    @{ Title='穆斯林的葬礼'; Author='霍达'; Category='文学'; Publisher='北京十月文艺出版社'; Year=2015 },
    @{ Title='额尔古纳河右岸'; Author='迟子建'; Category='文学'; Publisher='人民文学出版社'; Year=2019 },
    @{ Title='繁花'; Author='金宇澄'; Category='文学'; Publisher='上海文艺出版社'; Year=2013 },
    @{ Title='三体'; Author='刘慈欣'; Category='科幻'; Publisher='重庆出版社'; Year=2008 },
    @{ Title='球状闪电'; Author='刘慈欣'; Category='科幻'; Publisher='四川科学技术出版社'; Year=2016 },
    @{ Title='流浪地球'; Author='刘慈欣'; Category='科幻'; Publisher='长江文艺出版社'; Year=2017 },
    @{ Title='明朝那些事儿'; Author='当年明月'; Category='历史'; Publisher='北京联合出版公司'; Year=2017 },
    @{ Title='万历十五年'; Author='黄仁宇'; Category='历史'; Publisher='生活·读书·新知三联书店'; Year=2006 },
    @{ Title='中国通史'; Author='吕思勉'; Category='历史'; Publisher='中华书局'; Year=2015 },
    @{ Title='乡土中国'; Author='费孝通'; Category='社科'; Publisher='生活·读书·新知三联书店'; Year=2013 },
    @{ Title='论语译注'; Author='杨伯峻'; Category='国学'; Publisher='中华书局'; Year=2009 },
    @{ Title='诗经译注'; Author='程俊英'; Category='国学'; Publisher='上海古籍出版社'; Year=2012 },
    @{ Title='唐诗三百首'; Author='蘅塘退士'; Category='国学'; Publisher='中华书局'; Year=2016 },
    @{ Title='宋词三百首'; Author='朱祖谋'; Category='国学'; Publisher='中华书局'; Year=2016 },
    @{ Title='红楼梦'; Author='曹雪芹'; Category='古典文学'; Publisher='人民文学出版社'; Year=2008 },
    @{ Title='西游记'; Author='吴承恩'; Category='古典文学'; Publisher='人民文学出版社'; Year=2010 },
    @{ Title='三国演义'; Author='罗贯中'; Category='古典文学'; Publisher='人民文学出版社'; Year=2010 },
    @{ Title='水浒传'; Author='施耐庵'; Category='古典文学'; Publisher='人民文学出版社'; Year=2010 },
    @{ Title='人类简史'; Author='尤瓦尔·赫拉利'; Category='历史'; Publisher='中信出版社'; Year=2017 },
    @{ Title='未来简史'; Author='尤瓦尔·赫拉利'; Category='历史'; Publisher='中信出版社'; Year=2017 },
    @{ Title='枪炮、病菌与钢铁'; Author='贾雷德·戴蒙德'; Category='历史'; Publisher='中信出版社'; Year=2022 },
    @{ Title='时间简史'; Author='史蒂芬·霍金'; Category='科普'; Publisher='湖南科学技术出版社'; Year=2018 },
    @{ Title='寂静的春天'; Author='蕾切尔·卡逊'; Category='科普'; Publisher='商务印书馆'; Year=2020 },
    @{ Title='昆虫记'; Author='让-亨利·法布尔'; Category='科普'; Publisher='人民文学出版社'; Year=2018 },
    @{ Title='物种起源'; Author='查尔斯·达尔文'; Category='科普'; Publisher='商务印书馆'; Year=2009 },
    @{ Title='思考，快与慢'; Author='丹尼尔·卡尼曼'; Category='心理'; Publisher='中信出版社'; Year=2012 },
    @{ Title='影响力'; Author='罗伯特·西奥迪尼'; Category='心理'; Publisher='北京联合出版公司'; Year=2016 },
    @{ Title='原则'; Author='瑞·达利欧'; Category='管理'; Publisher='中信出版社'; Year=2018 },
    @{ Title='高效能人士的七个习惯'; Author='史蒂芬·柯维'; Category='管理'; Publisher='中国青年出版社'; Year=2018 },
    @{ Title='非暴力沟通'; Author='马歇尔·卢森堡'; Category='心理'; Publisher='华夏出版社'; Year=2018 },
    @{ Title='被讨厌的勇气'; Author='岸见一郎'; Category='心理'; Publisher='机械工业出版社'; Year=2017 },
    @{ Title='小王子'; Author='安托万·德·圣埃克苏佩里'; Category='外国文学'; Publisher='人民文学出版社'; Year=2018 },
    @{ Title='老人与海'; Author='欧内斯特·海明威'; Category='外国文学'; Publisher='上海译文出版社'; Year=2018 },
    @{ Title='百年孤独'; Author='加西亚·马尔克斯'; Category='外国文学'; Publisher='南海出版公司'; Year=2017 },
    @{ Title='变形记'; Author='弗兰茨·卡夫卡'; Category='外国文学'; Publisher='上海译文出版社'; Year=2018 },
    @{ Title='局外人'; Author='阿尔贝·加缪'; Category='外国文学'; Publisher='上海译文出版社'; Year=2018 },
    @{ Title='1984'; Author='乔治·奥威尔'; Category='外国文学'; Publisher='上海译文出版社'; Year=2009 },
    @{ Title='动物农场'; Author='乔治·奥威尔'; Category='外国文学'; Publisher='上海译文出版社'; Year=2009 },
    @{ Title='傲慢与偏见'; Author='简·奥斯汀'; Category='外国文学'; Publisher='译林出版社'; Year=2018 },
    @{ Title='简·爱'; Author='夏洛蒂·勃朗特'; Category='外国文学'; Publisher='译林出版社'; Year=2018 },
    @{ Title='悲惨世界'; Author='维克多·雨果'; Category='外国文学'; Publisher='人民文学出版社'; Year=2018 },
    @{ Title='战争与和平'; Author='列夫·托尔斯泰'; Category='外国文学'; Publisher='人民文学出版社'; Year=2018 },
    @{ Title='飘'; Author='玛格丽特·米切尔'; Category='外国文学'; Publisher='译林出版社'; Year=2018 },
    @{ Title='哈利·波特与魔法石'; Author='J.K.罗琳'; Category='外国文学'; Publisher='人民文学出版社'; Year=2017 },
    @{ Title='代码大全'; Author='史蒂夫·迈克康奈尔'; Category='计算机'; Publisher='电子工业出版社'; Year=2022 },
    @{ Title='深入理解计算机系统'; Author='兰德尔·布莱恩特'; Category='计算机'; Publisher='机械工业出版社'; Year=2016 },
    @{ Title='算法导论'; Author='托马斯·科尔曼'; Category='计算机'; Publisher='机械工业出版社'; Year=2013 },
    @{ Title='C# 核心技术指南'; Author='约瑟夫·阿尔巴哈里'; Category='计算机'; Publisher='人民邮电出版社'; Year=2020 },
    @{ Title='MySQL 技术内幕'; Author='姜承尧'; Category='计算机'; Publisher='机械工业出版社'; Year=2013 }
)

if ($books.Count -lt 50) {
    throw "图书清单不足 50 种。"
}

function Get-Isbn13 {
    param([int]$Sequence)
    $body = '9787{0:D8}' -f $Sequence
    $sum = 0
    for ($i = 0; $i -lt 12; $i++) {
        $digit = [int]::Parse($body[$i].ToString())
        $sum += $digit * $(if ($i % 2 -eq 0) { 1 } else { 3 })
    }
    $check = (10 - ($sum % 10)) % 10
    return "$body$check"
}

function ConvertTo-SqlText {
    param([string]$Value)
    return $Value.Replace('\', '\\').Replace("'", "''")
}

function Get-ShelfPrefix {
    param([string]$Category)
    switch ($Category) {
        '文学' { return 'A' }
        '古典文学' { return 'A' }
        '外国文学' { return 'B' }
        '科幻' { return 'C' }
        '历史' { return 'D' }
        '社科' { return 'E' }
        '国学' { return 'F' }
        '科普' { return 'G' }
        '心理' { return 'H' }
        '管理' { return 'I' }
        '计算机' { return 'T' }
        default { return 'Z' }
    }
}

$config = Get-OrCreateDatabaseConfig
$appOptionFile = New-AppOptionFile -Config $config
$sql = New-Object System.Text.StringBuilder
[void]$sql.AppendLine("USE ``$($config.database)``;")
[void]$sql.AppendLine("SET time_zone = '+00:00';")
[void]$sql.AppendLine('START TRANSACTION;')

for ($index = 0; $index -lt $books.Count; $index++) {
    $bookNumber = $index + 1
    $book = $books[$index]
    $isbn = Get-Isbn13 -Sequence $bookNumber
    $title = ConvertTo-SqlText $book.Title
    $author = ConvertTo-SqlText $book.Author
    $category = ConvertTo-SqlText $book.Category
    $publisher = ConvertTo-SqlText $book.Publisher
    $description = ConvertTo-SqlText ("{0}类馆藏：{1}，作者 {2}。" -f $book.Category, $book.Title, $book.Author)

    [void]$sql.AppendLine(@"
INSERT INTO books (isbn, title, author, category, publisher, publication_year, description, is_archived)
VALUES ('$isbn', '$title', '$author', '$category', '$publisher', $($book.Year), '$description', FALSE)
ON DUPLICATE KEY UPDATE
    id = LAST_INSERT_ID(id), title = VALUES(title), author = VALUES(author),
    category = VALUES(category), publisher = VALUES(publisher),
    publication_year = VALUES(publication_year), description = VALUES(description),
    is_archived = FALSE;
SET @book_id = LAST_INSERT_ID();
"@)

    $copyCount = 12 + ($bookNumber % 5)
    $shelfPrefix = Get-ShelfPrefix $book.Category
    $row = [int][Math]::Ceiling($bookNumber / 10.0)
    for ($copyNumber = 1; $copyNumber -le $copyCount; $copyNumber++) {
        $copyCode = 'LIB-{0:D3}-{1:D3}' -f $bookNumber, $copyNumber
        $shelf = '{0}-{1:D2}-{2:D2}' -f $shelfPrefix, $row, $copyNumber
        [void]$sql.AppendLine(@"
INSERT INTO book_copies (book_id, copy_code, shelf_location, is_disabled)
VALUES (@book_id, '$copyCode', '$shelf', FALSE)
ON DUPLICATE KEY UPDATE
    book_id = VALUES(book_id), shelf_location = VALUES(shelf_location), is_disabled = FALSE;
"@)
    }
}

[void]$sql.AppendLine('COMMIT;')
[void]$sql.AppendLine('SELECT COUNT(*) AS seeded_titles FROM books WHERE isbn LIKE ''9787%'';')
[void]$sql.AppendLine('SELECT MIN(copy_count) AS min_copies, MAX(copy_count) AS max_copies, SUM(copy_count) AS total_copies FROM (SELECT COUNT(*) AS copy_count FROM book_copies GROUP BY book_id) x;')

$result = Invoke-MySql -Sql $sql.ToString() -OptionFile $appOptionFile
Write-Host "已录入 $($books.Count) 种图书。" -ForegroundColor Green
Write-Host $result
